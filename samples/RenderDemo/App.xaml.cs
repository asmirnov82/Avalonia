// Copyright (c) The Avalonia Project. All rights reserved.
// Licensed under the MIT license. See licence.md file in the project root for full license information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Logging.Serilog;
using Avalonia.Markup.Xaml;

namespace RenderDemo
{
    public class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            AvaloniaXamlLoader.Load(this);
        }

        // TODO: Make this work with GTK/Skia/Cairo depending on command-line args
        // again.
        static void Main(string[] args) => BuildAvaloniaApp().Start<MainWindow>();

        // App configuration, used by the entry point and previewer
        static AppBuilder BuildAvaloniaApp()
           => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .UseReactiveUI()
                .LogToDebug();

    }
}
