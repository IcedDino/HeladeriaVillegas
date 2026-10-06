using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace HeladeriaPOS.Services;

/// <summary>Elevated executable inputs must never come from ordinary user-writable storage.</summary>
[SupportedOSPlatform("windows")]
public static class WindowsUpdateStorage
{
    private static readonly SecurityIdentifier Administrators = new(WellKnownSidType.BuiltinAdministratorsSid, null);
    private static readonly SecurityIdentifier SystemAccount = new(WellKnownSidType.LocalSystemSid, null);

    public static void EnsureSecure(string path)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(path));
        for (var ancestor = directory.Parent; ancestor is not null; ancestor = ancestor.Parent)
            RejectReparsePoint(ancestor);
        if (!directory.Exists)
        {
            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.SetOwner(Administrators);
            foreach (var identity in new[] { Administrators, SystemAccount })
                security.AddAccessRule(new FileSystemAccessRule(identity, FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            directory.Create(security);
        }
        Validate(directory);
        // Existing metadata/helper files need the same protection; never adopt an insecure directory.
        foreach (var entry in directory.EnumerateFileSystemInfos()) ValidateTree(entry);
    }

    private static void ValidateTree(FileSystemInfo entry)
    {
        Validate(entry);
        if (entry is DirectoryInfo directory)
            foreach (var child in directory.EnumerateFileSystemInfos()) ValidateTree(child);
    }

    private static void Validate(FileSystemInfo entry)
    {
        RejectReparsePoint(entry);
        FileSystemSecurity security = entry is DirectoryInfo directory
            ? directory.GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner)
            : ((FileInfo)entry).GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner);
        var owner = security.GetOwner(typeof(SecurityIdentifier));
        if (!Administrators.Equals(owner) && !SystemAccount.Equals(owner))
            throw new UnauthorizedAccessException("La carpeta de actualizaciones tiene un propietario no seguro.");
        bool adminAccess = false;
        foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
        {
            if (rule.AccessControlType != AccessControlType.Allow) continue;
            if (!Administrators.Equals(rule.IdentityReference) && !SystemAccount.Equals(rule.IdentityReference))
                throw new UnauthorizedAccessException("La carpeta de actualizaciones permite acceso a procesos sin elevar.");
            if (Administrators.Equals(rule.IdentityReference) && (rule.FileSystemRights & FileSystemRights.FullControl) == FileSystemRights.FullControl)
                adminAccess = true;
        }
        if (!adminAccess) throw new UnauthorizedAccessException("Faltan permisos elevados para actualizar.");
    }

    private static void RejectReparsePoint(FileSystemInfo entry)
    {
        if (entry.Exists && (entry.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new UnauthorizedAccessException("La ruta de actualizaciones no puede contener enlaces o redirecciones.");
    }
}
