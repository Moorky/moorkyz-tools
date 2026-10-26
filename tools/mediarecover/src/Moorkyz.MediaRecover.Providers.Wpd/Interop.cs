using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Moorkyz.Tools.MediaRecover.Providers.Wpd;

[StructLayout(LayoutKind.Sequential)]
internal struct PropertyKey(Guid format, uint id)
{
    public Guid Format = format;
    public uint Id = id;
}
[StructLayout(LayoutKind.Explicit, Size = 24)]
internal struct PropVariant
{
    [FieldOffset(0)] public ushort Type;
    [FieldOffset(8)] public long Long;
    [FieldOffset(8)] public double Double;
    [FieldOffset(8)] public IntPtr Pointer;
}
[ComImport, Guid("A1567595-4C2F-4574-A6FA-ECEF917B9A40"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDeviceManager
{
    void GetDevices(IntPtr ids, ref uint count);
    void RefreshDeviceList();
    void GetDeviceFriendlyName([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr name, ref uint chars);
    void GetDeviceDescription([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr name, ref uint chars);
    void GetDeviceManufacturer([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr name, ref uint chars);
}
[ComImport, Guid("625E2DF8-6392-4CF0-9AD1-3CFA5F17775C"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDevice
{
    void Open([MarshalAs(UnmanagedType.LPWStr)] string id, IValues values);
    void SendCommand(uint flags, IValues parameters, out IValues results);
    void Content(out IContent content);
    void Capabilities(out IntPtr capabilities);
    void Cancel();
    void Close();
}
[ComImport, Guid("6A96ED84-7C73-4480-9938-BF5AF477D426"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IContent
{
    void EnumObjects(uint flags, [MarshalAs(UnmanagedType.LPWStr)] string parent, IntPtr filter, out IObjectIds ids);
    void Properties(out IProperties properties);
    void Transfer(out IResources resources);
}
[ComImport, Guid("10ECE955-CF41-4728-BFA0-41EEDF1BBF19"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IObjectIds
{
    [PreserveSig] int Next(uint count, [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr, SizeParamIndex = 0), Out] string[] ids, out uint fetched);
    void Skip(uint count);
    void Reset();
    void Clone(out IObjectIds ids);
    void Cancel();
}
[ComImport, Guid("7F6D695C-03DF-4439-A809-59266BEEE3A6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IProperties
{
    void GetSupportedProperties([MarshalAs(UnmanagedType.LPWStr)] string id, out IntPtr keys);
    void GetPropertyAttributes([MarshalAs(UnmanagedType.LPWStr)] string id, ref PropertyKey key, out IValues attributes);
    void GetValues([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr keys, out IValues values);
}
[ComImport, Guid("6848F6F2-3155-4F86-B6F5-263EEEAB3143"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IValues
{
    void GetCount(out uint count);
    void GetAt(uint index, out PropertyKey key, out PropVariant value);
    void SetValue(ref PropertyKey key, ref PropVariant value);
    void GetValue(ref PropertyKey key, out PropVariant value);
    void SetStringValue(ref PropertyKey key, [MarshalAs(UnmanagedType.LPWStr)] string value);
    void GetStringValue(ref PropertyKey key, [MarshalAs(UnmanagedType.LPWStr)] out string value);
    void SetUnsignedIntegerValue(ref PropertyKey key, uint value);
    void GetUnsignedIntegerValue(ref PropertyKey key, out uint value);
    void SetSignedIntegerValue(ref PropertyKey key, int value);
    void GetSignedIntegerValue(ref PropertyKey key, out int value);
    void SetUnsignedLargeIntegerValue(ref PropertyKey key, ulong value);
    void GetUnsignedLargeIntegerValue(ref PropertyKey key, out ulong value);
}
[ComImport, Guid("FD8878AC-D841-4D17-891C-E6829CDB6934"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IResources
{
    void GetSupportedResources([MarshalAs(UnmanagedType.LPWStr)] string id, out IntPtr keys);
    void GetResourceAttributes([MarshalAs(UnmanagedType.LPWStr)] string id, ref PropertyKey key, out IValues attributes);
    void GetStream([MarshalAs(UnmanagedType.LPWStr)] string id, ref PropertyKey key, uint mode, ref uint optimalBuffer, out IStream stream);
}
internal static class Com
{
    internal static readonly Guid ObjectProperties = new("EF6B490D-5CD8-437A-AFFC-DA8B60EE4A3C");
    internal static readonly Guid Folder = new("27E2E392-A111-48E0-AB0C-E17705A05F85");
    internal static readonly Guid Functional = new("99ED0160-17FF-4C44-9D98-1D7A6F941921");
    internal static PropertyKey Object(uint id) => new(ObjectProperties, id);
    internal static T New<T>(string clsid) => (T)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid(clsid), true)!)!;
    internal static void Release(object? value) { if (value is not null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value); }
    [DllImport("ole32.dll")] internal static extern int PropVariantClear(ref PropVariant value);
    internal static string? String(IValues values, uint id)
    {
        var key = Object(id);
        try { values.GetStringValue(ref key, out var value); return value; } catch (COMException) { return null; }
    }
    internal static long? Size(IValues values)
    {
        var key = Object(11);
        try { values.GetUnsignedLargeIntegerValue(ref key, out var value); return value <= long.MaxValue ? (long)value : null; } catch (COMException) { return null; }
    }
    internal static Guid ContentType(IValues values)
    {
        var key = Object(7); PropVariant value = default;
        try { values.GetValue(ref key, out value); return value.Type == 72 && value.Pointer != IntPtr.Zero ? Marshal.PtrToStructure<Guid>(value.Pointer) : Guid.Empty; }
        catch (COMException) { return Guid.Empty; }
        finally { PropVariantClear(ref value); }
    }
    internal static bool Hidden(IValues values)
    {
        var key = Object(9); PropVariant value = default;
        try { values.GetValue(ref key, out value); return value.Type == 11 && (value.Long & 0xffff) != 0; }
        catch (COMException) { return false; }
        finally { PropVariantClear(ref value); }
    }
    internal static Core.DateValue Date(IValues values, uint id, string name)
    {
        var key = Object(id); PropVariant value = default;
        try
        {
            values.GetValue(ref key, out value);
            if (value.Type == 64)
            {
                var utc = DateTime.FromFileTimeUtc(value.Long);
                return new(name, value.Long.ToString(System.Globalization.CultureInfo.InvariantCulture), new DateTimeOffset(utc), "WPD FILETIME UTC; device fallback, not verified capture time");
            }
            if (value.Type == 7)
            {
                var local = DateTime.SpecifyKind(DateTime.FromOADate(value.Double), DateTimeKind.Unspecified);
                return new(name, value.Double.ToString(System.Globalization.CultureInfo.InvariantCulture), new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local)), "Device timezone unknown; PC offset used provisionally");
            }
            return new(name, $"VT={value.Type}", null, null);
        }
        catch (Exception e) when (e is COMException or ArgumentException) { return new(name, "Unavailable", null, null); }
        finally { PropVariantClear(ref value); }
    }
}
