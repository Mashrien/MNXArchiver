namespace Mash.UI.MNXFile.GUI;

internal static class TextPrompt {
    internal static string? Ask(Form owner, string title, string instruction, bool password = false) {
        using TextPromptForm dialog = new TextPromptForm(title, instruction, password);
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.Input : null;
        }

    internal static void ShowText(Form owner, string title, string text) {
        using TextViewerForm dialog = new TextViewerForm(title, text);
        dialog.ShowDialog(owner);
        }
    }
