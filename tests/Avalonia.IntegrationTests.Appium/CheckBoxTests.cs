﻿using OpenQA.Selenium.Appium;
using Xunit;

namespace Avalonia.IntegrationTests.Appium
{
    [Collection("Default")]
    public class CheckBoxTests
    {
        private readonly AppiumDriver<AppiumWebElement> _driver;

        public CheckBoxTests(DefaultAppFixture fixture)
        {
            _driver = fixture.Driver;

            var tabs = _driver.FindElementByAccessibilityId("MainTabs");
            var tab = tabs.FindElementByName("CheckBox");
            tab.Click();
        }

        [Fact]
        public void UncheckedCheckBox()
        {
            var checkBox = _driver.FindElementByAccessibilityId("UncheckedCheckBox");

            Assert.Equal("Unchecked", checkBox.GetName());
            Assert.Equal(false, checkBox.GetIsChecked());

            checkBox.Click();
            Assert.Equal(true, checkBox.GetIsChecked());
        }

        [Fact]
        public void CheckedCheckBox()
        {
            var checkBox = _driver.FindElementByAccessibilityId("CheckedCheckBox");

            Assert.Equal("Checked", checkBox.GetName());
            Assert.Equal(true, checkBox.GetIsChecked());

            checkBox.Click();
            Assert.Equal(false, checkBox.GetIsChecked());
        }

        [Fact]
        public void ThreeStateCheckBox()
        {
            var checkBox = _driver.FindElementByAccessibilityId("ThreeStateCheckBox");

            Assert.Equal("ThreeState", checkBox.GetName());
            Assert.Null(checkBox.GetIsChecked());

            checkBox.Click();
            Assert.Equal(false, checkBox.GetIsChecked());

            checkBox.Click();
            Assert.Equal(true, checkBox.GetIsChecked());

            checkBox.Click();
            Assert.Null(checkBox.GetIsChecked());
        }
    }
}
