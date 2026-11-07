using System;
using System.Windows;

namespace GrzmotoEditor;

/// <summary>Windows application entry point for VocaGrzmot.</summary>
public static class VocaGrzmot
{
    [STAThread]
    public static void Main()
    {
        var app = new Application
        {
            ShutdownMode = ShutdownMode.OnMainWindowClose
        };
        app.Run(new MainWindow());
    }
}
