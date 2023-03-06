﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia.Controls;
using Avalonia.IntegrationTests.Appium.Wrappers;
using Avalonia.Utilities;
using Avalonia.Media.Imaging;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Interactions;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using Xunit.Sdk;

namespace Avalonia.IntegrationTests.Appium
{
    [Collection("Default")]
    public class WindowTests
    {
        private readonly ISession _session;
        private readonly AppiumDriver<AppiumWebElement> _driver;
        private readonly IWindowElement _mainWindow;

        public WindowTests(DefaultAppFixture fixture)
        {
            _session = fixture.Session;
            _driver = fixture.Driver;

            _mainWindow = _session.GetWindow("MainWindow");

            var tabs = _mainWindow.FindElementByAccessibilityId("MainTabs");
            var tab = tabs.FindElementByName("Window");
            tab.Click();
        }

        [Theory]
        [MemberData(nameof(StartupLocationData))]
        public void StartupLocation(Size? size, ShowWindowMode mode, WindowStartupLocation location, bool canResize)
        {
            using var window = OpenWindow(size, mode, location, canResize: canResize);
            var info = GetWindowInfo(window);

            if (size.HasValue)
                Assert.Equal(size.Value, info.ClientSize);

            Assert.True(info.FrameSize.Width >= info.ClientSize.Width, "Expected frame width >= client width.");
            Assert.True(info.FrameSize.Height > info.ClientSize.Height, "Expected frame height > client height.");

            var frameRect = new PixelRect(info.Position, PixelSize.FromSize(info.FrameSize, info.Scaling));

            switch (location)
            {
                case WindowStartupLocation.CenterScreen:
                {
                    var expected = info.ScreenRect.CenterRect(frameRect);
                    AssertCloseEnough(expected.Position, frameRect.Position);
                    break;
                }
                case WindowStartupLocation.CenterOwner:
                {
                    Assert.NotNull(info.OwnerRect);
                    var expected = info.OwnerRect!.Value.CenterRect(frameRect);
                    AssertCloseEnough(expected.Position, frameRect.Position);
                    break;
                }
            }
        }

        [Theory]
        [MemberData(nameof(WindowStateData))]
        public void WindowState(Size? size, ShowWindowMode mode, WindowState state, bool canResize)
        {
            using var window = OpenWindow(size, mode, state: state, canResize: canResize);

            try
            {
                var info = GetWindowInfo(window);

                Assert.Equal(state, info.WindowState);

                switch (state)
                {
                    case Controls.WindowState.Normal:
                        Assert.True(info.FrameSize.Width * info.Scaling < info.ScreenRect.Size.Width);
                        Assert.True(info.FrameSize.Height * info.Scaling < info.ScreenRect.Size.Height);
                        break;
                    case Controls.WindowState.Maximized:
                    case Controls.WindowState.FullScreen:
                        Assert.True(info.FrameSize.Width * info.Scaling >= info.ScreenRect.Size.Width);
                        Assert.True(info.FrameSize.Height * info.Scaling >= info.ScreenRect.Size.Height);
                        break;
                }
            }
            finally
            {
                if (state == Controls.WindowState.FullScreen)
                {
                    try
                    {
                        window.FindElementByAccessibilityId("CurrentWindowState").SendClick();
                        window.FindElementByAccessibilityId("WindowStateNormal").SendClick();

                        // Wait for animations to run.
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                            Thread.Sleep(1000);
                    }
                    catch
                    {
                        // Ignore errors in cleanup 
                    }
                }
            }
        }

        [PlatformFact(TestPlatforms.Windows)]
        public void OnWindows_Docked_Windows_Retain_Size_Position_When_Restored()
        {
            using (var window = OpenWindow(new Size(400, 400), ShowWindowMode.NonOwned, WindowStartupLocation.Manual))
            {
                var windowState = window.FindElementByAccessibilityId("CurrentWindowState");

                Assert.Equal("Normal", windowState.GetComboBoxValue());
                
                
                new Actions(_driver)
                    .KeyDown(Keys.Meta)
                    .SendKeys(Keys.Left)
                    .KeyUp(Keys.Meta)
                    .Perform();
                
                var original = GetWindowInfo(window);
                
                windowState.Click();
                window.FindElementByName("Minimized").SendClick();
                
                new Actions(_driver)
                    .KeyDown(Keys.Alt)
                    .SendKeys(Keys.Tab)
                    .KeyUp(Keys.Alt)
                    .Perform();
                
                var current = GetWindowInfo(window);
                
                Assert.Equal(original.Position, current.Position);
                Assert.Equal(original.FrameSize, current.FrameSize);

            }
        }
        
        [Fact]
        public void Showing_Window_With_Size_Larger_Than_Screen_Measures_Content_With_Working_Area()
        {
            using (OpenWindow(new Size(4000, 2200), ShowWindowMode.NonOwned, WindowStartupLocation.Manual))
            {
                var screenRectTextBox = _driver.FindElementByAccessibilityId("CurrentClientSize");
                var measuredWithTextBlock = _driver.FindElementByAccessibilityId("CurrentMeasuredWithText");
                
                var measuredWithString = measuredWithTextBlock.Text;
                var workingAreaString = screenRectTextBox.Text;

                var workingArea = Size.Parse(workingAreaString);
                var measuredWith = Size.Parse(measuredWithString);

                Assert.Equal(workingArea, measuredWith);
            }
        }

        [Theory]
        [InlineData(ShowWindowMode.NonOwned)]
        [InlineData(ShowWindowMode.Owned)]
        [InlineData(ShowWindowMode.Modal)]
        public void ShowMode(ShowWindowMode mode)
        {
            using var window = OpenWindow(null, mode, WindowStartupLocation.Manual);
            var windowState = _mainWindow.FindElementByAccessibilityId("CurrentWindowState");
            var original = GetWindowInfo(window);

            Assert.Equal("Normal", windowState.GetComboBoxValue());

            windowState.Click();
            window.FindElementByAccessibilityId("WindowStateMaximized").SendClick();
            Assert.Equal("Maximized", windowState.GetComboBoxValue());

            windowState.Click();
            window.FindElementByAccessibilityId("WindowStateNormal").SendClick();

            var current = GetWindowInfo(window);
            Assert.Equal(original.Position, current.Position);
            Assert.Equal(original.FrameSize, current.FrameSize);

            // On macOS, only non-owned windows can go fullscreen.
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || mode == ShowWindowMode.NonOwned)
            {
                windowState.Click();
                window.FindElementByAccessibilityId("WindowStateFullScreen").SendClick();
                Assert.Equal("FullScreen", windowState.GetComboBoxValue());

                current = GetWindowInfo(window);
                var clientSize = PixelSize.FromSize(current.ClientSize, current.Scaling);
                Assert.True(clientSize.Width >= current.ScreenRect.Width);
                Assert.True(clientSize.Height >= current.ScreenRect.Height);

                windowState.SendClick();
                
                window.FindElementByAccessibilityId("WindowStateNormal").SendClick();

                current = GetWindowInfo(window);
                Assert.Equal(original.Position, current.Position);
                Assert.Equal(original.FrameSize, current.FrameSize);
            }
        }

        [Fact]
        public void TransparentWindow()
        {
            var showTransparentWindow = _mainWindow.FindElementByAccessibilityId("ShowTransparentWindow");
            showTransparentWindow.Click();
            Thread.Sleep(1000);

            var window = _mainWindow.FindElementByAccessibilityId("TransparentWindow");
            var screenshot = window.GetScreenshot();

            window.Click();

            var img = SixLabors.ImageSharp.Image.Load<Rgba32>(screenshot);
            var topLeftColor = img[10, 10];
            var centerColor = img[img.Width / 2, img.Height / 2];

            Assert.Equal(new Rgba32(0, 128, 0), topLeftColor);
            Assert.Equal(new Rgba32(255, 0, 0), centerColor);
        }

        [Fact]
        public void TransparentPopup()
        {
            var showTransparentWindow = _mainWindow.FindElementByAccessibilityId("ShowTransparentPopup");
            showTransparentWindow.Click();
            Thread.Sleep(1000);

            var window = _mainWindow.FindElementByAccessibilityId("TransparentPopupBackground");
            var container = window.FindElementByAccessibilityId("PopupContainer");
            var screenshot = container.GetScreenshot();

            window.Click();

            var img = SixLabors.ImageSharp.Image.Load<Rgba32>(screenshot);
            var topLeftColor = img[10, 10];
            var centerColor = img[img.Width / 2, img.Height / 2];

            Assert.Equal(new Rgba32(0, 128, 0), topLeftColor);
            Assert.Equal(new Rgba32(255, 0, 0), centerColor);
        }

        public static TheoryData<Size?, ShowWindowMode, WindowStartupLocation, bool> StartupLocationData()
        {
            var sizes = new Size?[] { null, new Size(400, 300) };
            var data = new TheoryData<Size?, ShowWindowMode, WindowStartupLocation, bool>();

            foreach (var size in sizes)
            {
                foreach (var mode in Enum.GetValues<ShowWindowMode>())
                {
                    foreach (var location in Enum.GetValues<WindowStartupLocation>())
                    {
                        if (!(location == WindowStartupLocation.CenterOwner && mode == ShowWindowMode.NonOwned))
                        {
                            data.Add(size, mode, location, true);
                            data.Add(size, mode, location, false);
                        }
                    }
                }
            }

            return data;
        }

        public static TheoryData<Size?, ShowWindowMode, WindowState, bool> WindowStateData()
        {
            var sizes = new Size?[] { null, new Size(400, 300) };
            var data = new TheoryData<Size?, ShowWindowMode, WindowState, bool>();

            foreach (var size in sizes)
            {
                foreach (var mode in Enum.GetValues<ShowWindowMode>())
                {
                    foreach (var state in Enum.GetValues<WindowState>())
                    {
                        // Not sure how to handle testing minimized windows currently.
                        if (state == Controls.WindowState.Minimized)
                            continue;

                        // Child/Modal windows cannot be fullscreen on macOS.
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) &&
                            state == Controls.WindowState.FullScreen &&
                            mode != ShowWindowMode.NonOwned)
                            continue;

                        data.Add(size, mode, state, true);
                        data.Add(size, mode, state, false);
                    }
                }
            }

            return data;
        }

        private static void AssertCloseEnough(PixelPoint expected, PixelPoint actual)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // On win32, accurate frame information cannot be obtained until a window is shown but
                // WindowStartupLocation needs to be calculated before the window is shown, meaning that
                // the position of a centered window can be off by a bit. From initial testing, looks
                // like this shouldn't be more than 10 pixels.
                if (Math.Abs(expected.X - actual.X) > 10)
                    throw new EqualException(expected, actual);
                if (Math.Abs(expected.Y - actual.Y) > 10)
                    throw new EqualException(expected, actual);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                if (Math.Abs(expected.X - actual.X) > 15)
                    throw new EqualException(expected, actual);
                if (Math.Abs(expected.Y - actual.Y) > 15)
                    throw new EqualException(expected, actual);
            }
            else
            {
                Assert.Equal(expected, actual);
            }
        }

        private IWindowElement OpenWindow(
            Size? size,
            ShowWindowMode mode,
            WindowStartupLocation location = WindowStartupLocation.Manual,
            WindowState state = Controls.WindowState.Normal,
            bool canResize = true)
        {
            var timer = new SplitTimer();
            
            var elements = _mainWindow.GetChildren();
            
            timer.SplitLog("getChildren");

            if (size.HasValue)
            {
                var sizeTextBox = _mainWindow.FindElementByAccessibilityId("ShowWindowSize");
                timer.SplitLog(nameof(sizeTextBox));
                sizeTextBox.SendKeys($"{size.Value.Width}, {size.Value.Height}");
            }

            var modeComboBox = _mainWindow.FindElementByAccessibilityId("ShowWindowMode");
            timer.SplitLog(nameof(modeComboBox));
            
            if (modeComboBox.GetComboBoxValue() != mode.ToString())
            {
                modeComboBox.Click();
                _mainWindow.FindElementByName(mode.ToString()).SendClick();
            }

            var locationComboBox = _mainWindow.FindElementByAccessibilityId("ShowWindowLocation");
            timer.SplitLog(nameof(locationComboBox));
            if (locationComboBox.GetComboBoxValue() != location.ToString())
            {
                locationComboBox.Click();
                _mainWindow.FindElementByName(location.ToString()).SendClick();
            }

            var stateComboBox = _mainWindow.FindElementByAccessibilityId("ShowWindowState");
            timer.SplitLog(nameof(stateComboBox));
            if (stateComboBox.GetComboBoxValue() != state.ToString())
            {
                stateComboBox.Click();
                _mainWindow.FindElementByAccessibilityId($"ShowWindowState{state}").SendClick();
            }

            var canResizeCheckBox = _mainWindow.FindElementByAccessibilityId("ShowWindowCanResize");
            timer.SplitLog(nameof(canResizeCheckBox));
            if (canResizeCheckBox.GetIsChecked() != canResize)
                canResizeCheckBox.Click();

            timer.Reset();
            
            var showButton = _mainWindow.FindElementByAccessibilityId("ShowWindow");
            timer.SplitLog(nameof(showButton));
            
            var result = _session.GetNewWindow(()=> showButton.Click());
            timer.SplitLog("GetNewWindow");

            return result;
        }

        private static WindowInfo GetWindowInfo(IWindowElement window)
        {
            var dictionary = new Dictionary<string, string>();
            
            PixelRect? ReadOwnerRect()
            {
                if (dictionary.ContainsKey("ownerrect"))
                {
                    return PixelRect.Parse(dictionary["ownerrect"]);
                }

                return null;
            }

            var retry = 0;

            for (;;)
            {
                try
                {
                    var timer = new SplitTimer();
                    var summary = window.FindElementByAccessibilityId("CurrentSummary").Text;

                    var items = summary.Split("::");

                    foreach (var item in items)
                    {
                        var kv = item.Split(":");

                        if (kv.Length == 2)
                        {
                            var key = kv[0];
                            var value = kv[1];

                            dictionary[key] = value;
                        }
                    }
                    
                    timer.SplitLog("summary");
                    
                    var result = new WindowInfo(
                        Size.Parse(dictionary["clientSize"]),
                        Size.Parse(dictionary["frameSize"]),
                        PixelPoint.Parse(dictionary["position"]),
                        ReadOwnerRect(),
                        PixelRect.Parse(dictionary["screen"]),
                        double.Parse(dictionary["scaling"]),
                        Enum.Parse<WindowState>(dictionary["windowstate"]));
                    
                    return result;
                }
                catch (OpenQA.Selenium.NoSuchElementException) when (retry++ < 3)
                {
                    // MacOS sometimes seems to need a bit of time to get itself back in order after switching out
                    // of fullscreen.
                    Thread.Sleep(1000);
                }
            }
        }

        public enum ShowWindowMode
        {
            NonOwned,
            Owned,
            Modal
        }

        private record WindowInfo(
            Size ClientSize,
            Size FrameSize,
            PixelPoint Position,
            PixelRect? OwnerRect,
            PixelRect ScreenRect,
            double Scaling,
            WindowState WindowState);
    }
}
