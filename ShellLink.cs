using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace Popfold;

/// <summary>
/// Atalhos (.lnk) com AppUserModelID próprio e listas de atalhos (jump list) da barra de tarefas.
/// Cada Popfold fixada tem um AppUserModelID diferente; sem isso o Windows trataria todos os
/// atalhos para o Popfold.exe como um só e não deixaria fixar mais de um.
/// </summary>
internal static class ShellLink
{
    public sealed record JumpItem(string Title, string Arguments, string IconPath);

    public static void Create(string lnkPath, string target, string arguments, string iconPath,
                              string description, string appUserModelId)
    {
        var link = (IShellLinkW)new CShellLink();
        try
        {
            link.SetPath(target);
            link.SetArguments(arguments);
            link.SetIconLocation(iconPath, 0);
            link.SetDescription(description);
            link.SetWorkingDirectory(System.IO.Path.GetDirectoryName(target) ?? "");
            SetString((IPropertyStore)link, AppUserModelIdKey, appUserModelId);
            ((IPersistFile)link).Save(lnkPath, true);
        }
        finally { Marshal.ReleaseComObject(link); }
    }

    /// <summary>Lê os argumentos de um atalho (para achar o atalho de uma Popfold). Null se falhar.</summary>
    public static string? ReadArguments(string lnkPath)
    {
        var link = (IShellLinkW)new CShellLink();
        try
        {
            ((IPersistFile)link).Load(lnkPath, 0);
            var sb = new StringBuilder(1024);
            link.GetArguments(sb, sb.Capacity);
            return sb.ToString();
        }
        catch { return null; }
        finally { Marshal.ReleaseComObject(link); }
    }

    /// <summary>
    /// Atualiza programa e ícone de um atalho existente (a cópia que o Windows guarda ao fixar), mantendo o resto.
    /// O programa muda quando o Popfold é reinstalado em outra pasta.
    /// </summary>
    public static void Retarget(string lnkPath, string target, string iconPath)
    {
        var link = (IShellLinkW)new CShellLink();
        try
        {
            ((IPersistFile)link).Load(lnkPath, 2 /* STGM_READWRITE */);
            link.SetPath(target);
            link.SetWorkingDirectory(System.IO.Path.GetDirectoryName(target) ?? "");
            link.SetIconLocation(iconPath, 0);
            ((IPersistFile)link).Save(lnkPath, true);
        }
        finally { Marshal.ReleaseComObject(link); }
    }

    /// <summary>
    /// Monta a lista que aparece ao clicar com o botão direito no ícone fixado.
    /// Os itens vão numa categoria com o nome da pasta; se o Windows recusar (o usuário removeu algum
    /// item da lista antes), vão para "Tarefas".
    /// </summary>
    public static void SetJumpList(string appUserModelId, string category, string exe, IReadOnlyList<JumpItem> items)
    {
        var list = (ICustomDestinationList)new CDestinationList();
        try
        {
            list.SetAppID(appUserModelId);
            var iid = typeof(IObjectArray).GUID;
            list.BeginList(out _, ref iid, out object removed);
            Marshal.ReleaseComObject(removed);

            if (items.Count > 0)
            {
                var collection = (IObjectCollection)new CEnumerableObjectCollection();
                foreach (var it in items)
                {
                    var link = (IShellLinkW)new CShellLink();
                    link.SetPath(exe);
                    link.SetArguments(it.Arguments);
                    link.SetIconLocation(it.IconPath, 0);
                    link.SetDescription(it.Title);
                    SetString((IPropertyStore)link, TitleKey, it.Title);
                    collection.AddObject(link);
                }
                try { list.AppendCategory(category, (IObjectArray)collection); }
                catch { list.AddUserTasks((IObjectArray)collection); }
                Marshal.ReleaseComObject(collection);
            }
            list.CommitList();
        }
        catch { try { list.AbortList(); } catch { } throw; }
        finally { Marshal.ReleaseComObject(list); }
    }

    public static void DeleteJumpList(string appUserModelId)
    {
        var list = (ICustomDestinationList)new CDestinationList();
        try { list.DeleteList(appUserModelId); }
        catch { }
        finally { Marshal.ReleaseComObject(list); }
    }

    // ---------- Propriedades ----------

    private static readonly PropertyKey AppUserModelIdKey = new(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);
    private static readonly PropertyKey TitleKey = new(new Guid("F29F85E0-4FF9-1068-AB91-08002B27B3D9"), 2);

    private static void SetString(IPropertyStore store, PropertyKey key, string value)
    {
        var pv = new PropVariant { vt = 31 /* VT_LPWSTR */, pointer = Marshal.StringToCoTaskMemUni(value) };
        try
        {
            store.SetValue(ref key, ref pv);
            store.Commit();
        }
        finally { Marshal.FreeCoTaskMem(pv.pointer); }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private readonly struct PropertyKey
    {
        public readonly Guid FormatId;
        public readonly int PropertyId;
        public PropertyKey(Guid f, int p) { FormatId = f; PropertyId = p; }
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct PropVariant
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public IntPtr pointer;
    }

    // ---------- COM ----------

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class CShellLink { }

    [ComImport, Guid("77F10CF0-3DB5-4966-B520-B7C54FD35ED6")]
    private class CDestinationList { }

    [ComImport, Guid("2D3468C1-36A7-43B6-AC24-D3F02FD9607A")]
    private class CEnumerableObjectCollection { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int cch, IntPtr findData, uint flags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder dir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string dir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder args, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int showCmd);
        void SetShowCmd(int showCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int cch, out int index);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path, int index);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(IntPtr hwnd, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    private interface IPropertyStore
    {
        void GetCount(out uint count);
        void GetAt(uint index, out PropertyKey key);
        void GetValue(ref PropertyKey key, out PropVariant value);
        void SetValue(ref PropertyKey key, ref PropVariant value);
        void Commit();
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("92CA9DCD-5622-4BBA-A805-5E9F541BD8C9")]
    private interface IObjectArray
    {
        void GetCount(out uint count);
        void GetAt(uint index, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object obj);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("5632B1A4-E38A-400A-928A-D4CD63230295")]
    private interface IObjectCollection
    {
        void GetCount(out uint count);
        void GetAt(uint index, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object obj);
        void AddObject([MarshalAs(UnmanagedType.Interface)] object obj);
        void AddFromArray(IObjectArray source);
        void RemoveObjectAt(uint index);
        void Clear();
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("6332DEBF-87B5-4670-90C0-5E57B408A49E")]
    private interface ICustomDestinationList
    {
        void SetAppID([MarshalAs(UnmanagedType.LPWStr)] string appId);
        void BeginList(out uint minSlots, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object removed);
        void AppendCategory([MarshalAs(UnmanagedType.LPWStr)] string category, IObjectArray items);
        void AppendKnownCategory(int category);
        void AddUserTasks(IObjectArray tasks);
        void CommitList();
        void GetRemovedDestinations(ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object removed);
        void DeleteList([MarshalAs(UnmanagedType.LPWStr)] string appId);
        void AbortList();
    }
}
