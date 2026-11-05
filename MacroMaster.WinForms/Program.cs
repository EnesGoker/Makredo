using MacroMaster.WinForms.Composition;

namespace MacroMaster.WinForms;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        using AppCompositionRoot compositionRoot = AppCompositionRoot.Create();
        using var globalExceptionHandlerRegistration = new GlobalExceptionHandlerRegistration(
            compositionRoot.AppLogger);
        global::System.Windows.Forms.Application.Run(compositionRoot.CreateMainForm());
    }
}
