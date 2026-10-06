using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using CasaMiga.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.PhotoTests;

[TestClass, DoNotParallelize]
public sealed class PasswordInputPresentationTests
{
    private static T Inner<T>(PasswordInput input) where T : FrameworkElement => (T)input.FindName(typeof(T) == typeof(PasswordBox) ? "Hidden" : "Plain");
    private static ToggleButton Eye(PasswordInput input) => (ToggleButton)input.FindName("Reveal");

    [TestMethod]
    public Task RevealShowsSamePasswordAndHidingRestoresIt() => WpfTestHost.OnUi(() =>
    {
        var input = new PasswordInput { MaxLength = 16 };
        WpfTestHost.Layout(input, 400, 60);
        var changes = 0; input.PasswordChanged += (_, _) => changes++;
        Inner<PasswordBox>(input).Password = "Panaderia#2026";
        Assert.AreEqual(1, changes);

        Eye(input).IsChecked = true;
        Assert.IsTrue(input.IsRevealed);
        Assert.AreEqual(Visibility.Visible, Inner<TextBox>(input).Visibility);
        Assert.AreEqual(Visibility.Collapsed, Inner<PasswordBox>(input).Visibility);
        Assert.AreEqual("Panaderia#2026", Inner<TextBox>(input).Text);
        Assert.AreEqual("", Inner<PasswordBox>(input).Password, "Only the visible field keeps the value.");
        Assert.AreEqual("Panaderia#2026", input.Password);
        Assert.AreEqual(1, changes, "Revealing is not a password change.");
        Assert.AreEqual("Ocultar contraseña", Eye(input).ToolTip);

        Inner<TextBox>(input).Text = "Panaderia#2027";
        Assert.AreEqual(2, changes);
        Eye(input).IsChecked = false;
        Assert.AreEqual("Panaderia#2027", input.Password);
        Assert.AreEqual("", Inner<TextBox>(input).Text, "Plain text is cleared when hidden again.");
        Assert.AreEqual(16, Inner<TextBox>(input).MaxLength);

        input.Clear();
        Assert.AreEqual("", input.Password);
        Assert.AreEqual(3, changes);
    });

    [TestMethod]
    public Task RevealedPasswordCannotBeCopiedAndKeepsNoUndoHistory() => WpfTestHost.OnUi(() =>
    {
        var input = new PasswordInput();
        WpfTestHost.Layout(input, 400, 60);
        input.Password = "Secreto#2026";
        Eye(input).IsChecked = true;
        var plain = Inner<TextBox>(input);
        Assert.IsFalse(plain.IsUndoEnabled);
        Assert.IsNull(plain.ContextMenu);
        plain.SelectAll();
        // Sin tocar el portapapeles real del equipo: los comandos quedan deshabilitados y, si se invocan, no actúan.
        Assert.IsFalse(ApplicationCommands.Copy.CanExecute(null, plain));
        Assert.IsFalse(ApplicationCommands.Cut.CanExecute(null, plain));
        ApplicationCommands.Cut.Execute(null, plain);
        Assert.AreEqual("Secreto#2026", input.Password, "Cut must not remove the text.");
    });

    [TestMethod]
    public Task ChecklistIsNeutralThenGreenOrRedAsTheUserTypes() => WpfTestHost.OnUi(() =>
    {
        var checklist = new PasswordChecklist();
        Assert.IsTrue(checklist.Current.All(i => i.State == InputState.Neutral), "Nothing is red before typing.");
        Assert.AreEqual(6, checklist.Current.Count);

        var (password, confirmation) = checklist.Update("panaderia", "");
        Assert.AreEqual(InputState.Invalid, password);
        Assert.AreEqual(InputState.Neutral, confirmation);
        CollectionAssert.AreEqual(new[] { InputState.Valid, InputState.Invalid, InputState.Valid, InputState.Invalid, InputState.Invalid },
            checklist.Current.Take(5).Select(i => i.State).ToArray());

        (password, confirmation) = checklist.Update("Panaderia#2026", "Panaderia#2025");
        Assert.AreEqual(InputState.Valid, password);
        Assert.AreEqual(InputState.Invalid, confirmation);
        Assert.AreEqual("Las contraseñas no coinciden", checklist.Current[^1].Label);
        Assert.IsFalse(checklist.IsSatisfied);

        (password, confirmation) = checklist.Update("Panaderia#2026", "Panaderia#2026");
        Assert.AreEqual(InputState.Valid, confirmation);
        Assert.AreEqual("Las contraseñas coinciden", checklist.Current[^1].Label);
        Assert.IsTrue(checklist.IsSatisfied);
        Assert.IsTrue(checklist.Current.All(i => i.Glyph == "✓"));
    });

    [TestMethod]
    public Task BootstrapFormEnablesCreateOnlyWhenEveryRuleIsMetAndColorsFields() => WpfTestHost.OnUi(() =>
    {
        var window = new MainWindow { DataContext = new MainViewModel(), Left = -10000, Top = -10000, ShowInTaskbar = false, ShowActivated = false };
        try
        {
            window.Show(); WpfTestHost.Layout((FrameworkElement)window.Content, 1280, 820);
            var password = (PasswordInput)window.FindName("BootstrapPassword");
            var confirmation = (PasswordInput)window.FindName("BootstrapConfirmation");
            var create = (Button)window.FindName("BootstrapButton");
            Assert.IsFalse(create.IsEnabled);

            password.Password = "panaderia";
            Assert.IsFalse(create.IsEnabled);
            Assert.AreEqual(InputState.Invalid, password.State);
            Assert.AreEqual("Invalid", Inner<PasswordBox>(password).Tag, "The theme paints the border from Tag.");

            password.Password = "Panaderia#2026"; confirmation.Password = "Panaderia#2026";
            Assert.IsTrue(create.IsEnabled);
            Assert.AreEqual(InputState.Valid, password.State);
            Assert.AreEqual(InputState.Valid, confirmation.State);

            confirmation.Password = "Panaderia#20";
            Assert.IsFalse(create.IsEnabled);
            Assert.AreEqual(InputState.Invalid, confirmation.State);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public Task ThemedInputsKeepPaddingAndShowBrandRingOrValidationColor() => WpfTestHost.OnUi(() =>
    {
        var box = new TextBox { Text = "abc", Width = 300 };
        var window = new Window { Content = box, Left = -10000, Top = -10000, Width = 400, Height = 200, ShowInTaskbar = false };
        try
        {
            window.Show(); window.Activate(); WpfTestHost.Layout(box, 300, 46);
            var frame = (Border)box.Template.FindName("Frame", box);
            var ring = (Border)box.Template.FindName("Ring", box);
            var text = Descendant(box, "TextBoxView");
            var origin = text.TransformToAncestor(box).Transform(new Point());
            Assert.IsTrue(origin.X >= 13 && origin.Y >= 9, $"Padding 12,8 must still apply (text at {origin}).");
            Assert.AreEqual(Visibility.Collapsed, ring.Visibility);

            Keyboard.Focus(box); WpfTestHost.Layout(box, 300, 46);
            Assert.IsTrue(box.IsKeyboardFocused);
            Assert.AreEqual(Visibility.Visible, ring.Visibility, "Selected inputs show the brand ring.");
            Assert.AreEqual(((SolidColorBrush)System.Windows.Application.Current.Resources["PrimaryBrush"]).Color, ((SolidColorBrush)ring.BorderBrush).Color);

            box.Tag = "Invalid"; WpfTestHost.Layout(box, 300, 46);
            Assert.AreEqual(((SolidColorBrush)System.Windows.Application.Current.Resources["ErrorBrush"]).Color, ((SolidColorBrush)frame.BorderBrush).Color);
            box.Tag = "Valid"; WpfTestHost.Layout(box, 300, 46);
            Assert.AreEqual(((SolidColorBrush)System.Windows.Application.Current.Resources["ValidBrush"]).Color, ((SolidColorBrush)frame.BorderBrush).Color);
        }
        finally { window.Close(); }
    });

    private static FrameworkElement Descendant(DependencyObject root, string typeName)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child.GetType().Name == typeName) return (FrameworkElement)child;
            if (Descendant(child, typeName) is { } found) return found;
        }
        return null!;
    }
}
