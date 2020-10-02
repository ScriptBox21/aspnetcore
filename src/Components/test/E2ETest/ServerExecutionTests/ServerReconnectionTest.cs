// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Threading;
using BasicTestApp;
using BasicTestApp.Reconnection;
using Microsoft.AspNetCore.Components.E2ETest.Infrastructure;
using Microsoft.AspNetCore.Components.E2ETest.Infrastructure.ServerFixtures;
using Microsoft.AspNetCore.E2ETesting;
using OpenQA.Selenium;
using TestServer;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Components.E2ETest.ServerExecutionTests
{
    public class ServerReconnectionTest : ServerTestBase<BasicTestAppServerSiteFixture<ServerStartup>>
    {
        public ServerReconnectionTest(
            BrowserFixture browserFixture,
            BasicTestAppServerSiteFixture<ServerStartup> serverFixture,
            ITestOutputHelper output)
            : base(browserFixture, serverFixture, output)
        {
        }

        protected override void InitializeAsyncCore()
        {
            Navigate(ServerPathBase, noReload: false);
            Browser.MountTestComponent<ReconnectionComponent>();
            Browser.Exists(By.Id("count"));
        }

        [Fact]
        public void ReconnectUI()
        {
            Browser.Exists(By.Id("increment")).Click();

            var javascript = (IJavaScriptExecutor)Browser;
            javascript.ExecuteScript("Blazor._internal.forceCloseConnection()");

            // We should see the 'reconnecting' UI appear
            Browser.Equal("block", () => Browser.Exists(By.Id("components-reconnect-modal")).GetCssValue("display"));

            // Then it should disappear
            Browser.Equal("none", () => Browser.Exists(By.Id("components-reconnect-modal")).GetCssValue("display"));

            Browser.Exists(By.Id("increment")).Click();

            // Can dispatch events after reconnect
            Browser.Equal("2", () => Browser.Exists(By.Id("count")).Text);
        }

        [Fact]
        public void RendersContinueAfterReconnect()
        {
            var selector = By.Id("ticker");
            var element = Browser.Exists(selector);

            var initialValue = element.Text;

            var javascript = (IJavaScriptExecutor)Browser;
            javascript.ExecuteScript("Blazor._internal.forceCloseConnection()");

            // We should see the 'reconnecting' UI appear
            Browser.Equal("block", () => Browser.Exists(By.Id("components-reconnect-modal")).GetCssValue("display"));

            // Then it should disappear
            Browser.Equal("none", () => Browser.Exists(By.Id("components-reconnect-modal")).GetCssValue("display"));

            // We should receive a render that occurred while disconnected
            var currentValue = Browser.Exists(selector).Text;
            Assert.NotEqual(initialValue, currentValue);

            // Verify it continues to tick
            Thread.Sleep(5);
            Browser.False(() => Browser.Exists(selector).Text == currentValue);
        }

        [Fact(Skip = "Browser logs cannot be retrieved: https://github.com/dotnet/aspnetcore/issues/25803")]
        public void ErrorsStopTheRenderingProcess()
        {
            Browser.Exists(By.Id("cause-error")).Click();
            Browser.True(() => Browser.Manage().Logs.GetLog(LogType.Browser)
                .Any(l => l.Level == LogLevel.Info && l.Message.Contains("Connection disconnected.")));
        }
    }
}