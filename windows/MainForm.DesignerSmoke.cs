using System.ComponentModel;

namespace Mash.GUI.MNXArchiver;

public sealed partial class MainForm {
    private static void RunDesignerConstructionChecks(string directory) {
        // The VS property grid uses named cultures when converting numeric properties.
        System.Globalization.CultureInfo designerCulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        PropertyDescriptor radiusProperty = TypeDescriptor.GetProperties(new MashWin.MWindowSettings().Frame)["CornerRadius"]!;
        Require((int)radiusProperty.Converter.ConvertFromString(null, designerCulture, "6")! == 6,
            "Designer numeric property conversion is unavailable.");
        LicenseContext previousContext = LicenseManager.CurrentContext;
        try {
            LicenseManager.CurrentContext = new DesignerLicenseContext();
            Form[] forms = new Form[] {
                new MainForm(), new InputSelectionForm(), new CreationForm(),
                new ProgressForm(), new OptionsForm(), new PreviewForm(),
                new ArchiveDropForm(), new TextPromptForm(), new TextViewerForm(), new CancellationForm()
                };
            foreach (Form form in forms) {
                using (form) {
                    if (form is MainForm main) {
                        Require(main.rootButton.TextAlign == ContentAlignment.MiddleCenter
                            && main.upButton.TextAlign == ContentAlignment.MiddleCenter,
                            "Designer save discarded navigation button text alignment.");
                        Require(main.mainToolbar.Renderer is MashWin.MToolStripRenderer,
                            "Main toolbar did not receive MWin styling in design mode.");
                        Require(ReferenceEquals(main.mainMenu.Renderer, main.mainToolbar.Renderer)
                            && ReferenceEquals(main.entryContextMenu.Renderer, main.mainToolbar.Renderer),
                            "Main menus and toolbar do not share the MWin renderer.");
                        ToolStripButton[] toolbarButtons = new ToolStripButton[] {
                            main.newArchiveButton, main.openArchiveButton, main.addFilesButton,
                            main.extractButton, main.deleteButton, main.verifyButton,
                            main.compactButton, main.propertiesButton
                            };
                        foreach (ToolStripButton toolbarButton in toolbarButtons) {
                            Require(toolbarButton.Image is not null, toolbarButton.Text + " icon is missing before runtime initialization.");
                            }
                        }
                    Require(form is MashWin.MWindow, form.Name + " still uses a stock Form base.");
                    form.Show();
                    Application.DoEvents();
                    form.CreateControl();
                    form.PerformLayout();
                    Require(form.Controls.Count > 0, form.Name + " has no designer controls.");
                    using Bitmap bitmap = new Bitmap(form.Width, form.Height);
                    form.DrawToBitmap(bitmap, form.ClientRectangle);
                    bitmap.Save(Path.Combine(directory, "designer-" + form.GetType().Name + ".png"));
                    }
                }
            }
        finally {
            LicenseManager.CurrentContext = previousContext;
            }
        Require(typeof(MainForm).BaseType == typeof(MashWin.MWindow), "MainForm is not based on MashWin.");
        using MashWin.MWindow customWindow = new MashWin.MWindow();
        customWindow.Size = new Size(600, 400);
        using Bitmap drawing = new Bitmap(600, 400);
        customWindow.Show();
        customWindow.Settings.CaptionButtons.ExitBox = false;
        customWindow.Size = new Size(601, 400);
        customWindow.DrawToBitmap(drawing, customWindow.ClientRectangle);
        Require(!customWindow.Controls["rbExit"]!.Visible, "MWin did not hide the disabled caption button.");
        customWindow.Settings.CaptionButtons.ExitBox = true;
        customWindow.BackColor = Color.Azure;
        customWindow.Size = new Size(600, 400);
        customWindow.DrawToBitmap(drawing, customWindow.ClientRectangle);
        Require(customWindow.Controls["rbExit"]!.Visible, "MWin did not restore its caption button.");
        customWindow.Settings.Frame.CornerRadius = 0;
        customWindow.Settings.Title.ShowTitle = false;
        customWindow.Size = new Size(599, 400);
        customWindow.DrawToBitmap(drawing, customWindow.ClientRectangle);
        customWindow.Close();
        PropertyDescriptor borderThickness = TypeDescriptor.GetProperties(customWindow.Settings.Frame)["BorderThickness"]!;
        Require(!borderThickness.ShouldSerializeValue(customWindow.Settings.Frame), "MWin default metadata does not match its property value.");
        using MashWin.RoundButton button = new MashWin.RoundButton();
        button.Size = new Size(80, 30);
        using Bitmap buttonDrawing = new Bitmap(80, 30);
        button.DrawToBitmap(buttonDrawing, button.ClientRectangle);
        }

    private sealed class DesignerLicenseContext : LicenseContext {
        public override LicenseUsageMode UsageMode => LicenseUsageMode.Designtime;
        }
    }
