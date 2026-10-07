using System.Xml.Linq;
using CasaMiga.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

[TestClass]
public sealed class SignInScreenTests
{
    [TestMethod]
    public void ClockFormatsTimeDateAndGreetingInSpanish()
    {
        var morning = new DateTime(2026, 10, 6, 7, 5, 0);
        Assert.AreEqual("07:05", SignInClock.Time(morning));
        Assert.AreEqual("Martes 6 de octubre", SignInClock.Date(morning));
        Assert.AreEqual("Buenos días", SignInClock.Greeting(morning));
        Assert.AreEqual("Buenos días", SignInClock.Greeting(morning.Date.AddHours(5)));
        Assert.AreEqual("Buenas tardes", SignInClock.Greeting(morning.Date.AddHours(12)));
        Assert.AreEqual("Buenas tardes", SignInClock.Greeting(morning.Date.AddHours(18).AddMinutes(59)));
        Assert.AreEqual("Buenas noches", SignInClock.Greeting(morning.Date.AddHours(19)));
        Assert.AreEqual("Buenas noches", SignInClock.Greeting(morning.Date.AddHours(4).AddMinutes(59)));
        Assert.AreEqual("Sábado 10 de octubre", SignInClock.Date(new DateTime(2026, 10, 10)));
    }

    [TestMethod]
    public void SignInPanelShowsClockAndStatusWithoutSensitiveData()
    {
        var shell = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Presentation", "MainWindow.xaml"));
        var signedOut = XDocument.Parse(shell).Descendants().First(e => e.Name.LocalName == "ScrollViewer" && (string?)e.Attribute("Visibility") == "{Binding IsSignedOut,Converter={StaticResource VisibleWhen}}").ToString();
        foreach (var binding in new[] { "{Binding SignInTime}", "{Binding SignInDate}", "{Binding SignInCashLabel}", "{Binding StatusLabel}", "{Binding CurrentTerminalName}", "SignInChip", "CasaMigaLogo.png" })
            StringAssert.Contains(signedOut, binding);
        // Antes de autenticar no se muestran importes ni nombres de usuarios.
        foreach (var hidden in new[] { "CashDetails", "ClosingOwner", "CurrentUserName", "Users" })
            Assert.IsFalse(signedOut.Contains("{Binding " + hidden, StringComparison.Ordinal), hidden);
        foreach (var name in new[] { "LoginUser", "LoginPassword", "BootstrapPassword", "BootstrapConfirmation", "BootstrapButton", "ChangeNew", "ChangeSaveButton" })
            StringAssert.Contains(signedOut, $"x:Name=\"{name}\"");
    }
}
