using System.Security.Principal;

namespace ResetExplode;

public static class EnergyAutostart
{
    private static string TaskName => "ResetExplode-Monitor-" + WindowsIdentity.GetCurrent().User!.Value;
    public static bool IsEnabled()
    {
        try
        {
            dynamic service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")!)!;
            service.Connect(); return service.GetFolder("\\").GetTask(TaskName).Enabled;
        }
        catch { return false; }
    }
    public static void SetEnabled(bool enabled, string? executablePath = null)
    {
        dynamic service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")!)!;
        service.Connect(); dynamic folder = service.GetFolder("\\");
        if (!enabled) { if (IsEnabled()) folder.DeleteTask(TaskName, 0); return; }
        string path = System.IO.Path.GetFullPath(executablePath ?? Environment.ProcessPath!);
        if (!System.IO.File.Exists(path) || !string.Equals(System.IO.Path.GetFileName(path), "ResetExplode.exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Disponível no aplicativo instalado.");
        string sid = WindowsIdentity.GetCurrent().User!.Value;
        dynamic definition = service.NewTask(0);
        definition.RegistrationInfo.Description = "Inicia RESET EXPLODE na bandeja, em segundo plano, no logon do usuário.";
        definition.Principal.UserId = sid; definition.Principal.LogonType = 3; definition.Principal.RunLevel = 1;
        dynamic trigger = definition.Triggers.Create(9); trigger.UserId = sid;
        dynamic action = definition.Actions.Create(0); action.Path = path;
        action.Arguments = "--background"; action.WorkingDirectory = System.IO.Path.GetDirectoryName(path);
        definition.Settings.Enabled = true; definition.Settings.StartWhenAvailable = true;
        definition.Settings.DisallowStartIfOnBatteries = false; definition.Settings.StopIfGoingOnBatteries = false;
        definition.Settings.ExecutionTimeLimit = "PT0S"; definition.Settings.MultipleInstances = 2;
        folder.RegisterTaskDefinition(TaskName, definition, 6, sid, null, 3);
    }
}
