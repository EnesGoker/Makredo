using System.Reflection;
using Makredo.Application.Abstractions;
using Makredo.Windows.Tests.Hooks;
using Makredo.WinForms.Controls;
using Xunit;

namespace Makredo.Windows.Tests.Library;

public sealed class MacroLibraryInteractionTests
{
    [Fact]
    public Task SingleClickSelectsAndDoubleClickOrEnterLoads() => HookThreadIntegrationTests.RunOnStaAsync(() =>
    {
        using var form = new Form { ShowInTaskbar = false, ClientSize = new Size(400, 460) };
        using var library = new MacroLibraryControl { Dock = DockStyle.Fill };
        form.Controls.Add(library);
        var loadedPaths = new List<string>();
        library.LoadRequested += (_, args) => loadedPaths.Add(args.Item.FilePath);
        string firstPath = Path.Combine(Path.GetTempPath(), "First.json");
        string secondPath = Path.Combine(Path.GetTempPath(), "Second.json");
        SetItems(library, firstPath, secondPath, firstPath);

        Control first = FindRow(library, "First");
        Control second = FindRow(library, "Second");
        Color selectedColor = first.BackColor;
        Color normalColor = second.BackColor;
        Assert.NotEqual(selectedColor, normalColor);
        Label secondLabel = Assert.Single(Descendants(second).OfType<Label>(), label => label.Text == "Second");
        var singleClick = new MouseEventArgs(MouseButtons.Left, 1, 2, 2, 0);
        InvokeControlEvent(secondLabel, "OnMouseClick", singleClick);

        Assert.Empty(loadedPaths);
        Assert.Equal(normalColor, first.BackColor);
        Assert.Equal(selectedColor, second.BackColor);

        SetItems(library, firstPath, secondPath, firstPath);
        second = FindRow(library, "Second");
        Assert.Equal(selectedColor, second.BackColor);
        secondLabel = Assert.Single(Descendants(second).OfType<Label>(), label => label.Text == "Second");
        InvokeControlEvent(secondLabel, "OnMouseDoubleClick", new MouseEventArgs(MouseButtons.Right, 2, 2, 2, 0));
        Assert.Empty(loadedPaths);

        InvokeControlEvent(secondLabel, "OnMouseDoubleClick", new MouseEventArgs(MouseButtons.Left, 2, 2, 2, 0));
        Assert.Equal(secondPath, Assert.Single(loadedPaths));

        first = FindRow(library, "First");
        InvokeControlEvent(first, "OnEnter", EventArgs.Empty);
        Assert.Equal(selectedColor, first.BackColor);
        Assert.True(first.TabStop);
        Assert.True((bool)typeof(Control).GetMethod("IsInputKey", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(first, [Keys.Enter])!);
        var enter = new KeyEventArgs(Keys.Enter);
        InvokeControlEvent(first, "OnKeyDown", enter);
        Assert.True(enter.SuppressKeyPress);

        SetItems(library, firstPath, secondPath, secondPath);
        Assert.Equal(selectedColor, FindRow(library, "Second").BackColor);
        Assert.Equal([secondPath, firstPath], loadedPaths);
        return Task.CompletedTask;
    });

    private static void SetItems(MacroLibraryControl library, string firstPath, string secondPath, string selectedPath)
    {
        MethodInfo setItems = typeof(MacroLibraryControl).GetMethod(nameof(MacroLibraryControl.SetItems))!;
        Type itemType = setItems.GetParameters()[0].ParameterType.GetGenericArguments()[0];
        Array items = Array.CreateInstance(itemType, 2);
        items.SetValue(CreateViewItem(itemType, "First", firstPath), 0);
        items.SetValue(CreateViewItem(itemType, "Second", secondPath), 1);
        setItems.Invoke(library, [items, selectedPath]);
    }

    private static object? CreateViewItem(Type itemType, string name, string filePath)
    {
        var entry = new MacroLibraryEntry(name, filePath, DateTime.UnixEpoch, 2, 0, MacroLibraryFileFormat.Json);
        return Activator.CreateInstance(itemType, entry, false, null);
    }

    private static Control FindRow(Control library, string name)
        => Assert.Single(Descendants(library), control => control.AccessibleRole == AccessibleRole.ListItem && control.AccessibleName == name);

    private static IEnumerable<Control> Descendants(Control parent)
        => parent.Controls.Cast<Control>().SelectMany(control => Descendants(control).Prepend(control));

    private static void InvokeControlEvent(Control control, string method, EventArgs args)
        => typeof(Control).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(control, [args]);
}
