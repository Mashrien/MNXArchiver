using System.Drawing;
using System.Reflection;
using Majorsilence.Forms;
using Majorsilence.Forms.Drawing;
using Majorsilence.Forms.Headless;
using MashWin;
using Mash.GUI.MNXArchiver;

internal static class Program {
    private static int Main(string[] arguments) {
        string work = Path.GetFullPath(arguments[0]);
        Directory.CreateDirectory(work);
        HeadlessRenderer.Use();
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        Application.SetDefaultFont(new Font("Noto Sans", 9));
        Type settingsType = typeof(MainForm).Assembly.GetType("Mash.GUI.MNXArchiver.Settings.AppSettings", true)!;
        object settings = Activator.CreateInstance(settingsType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, new object[] { Path.Combine(work, "settings") }, null)!;
        ConstructorInfo constructor = typeof(MainForm).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
            null, new Type[] { settingsType }, null) ?? throw new InvalidOperationException("Missing isolated-settings constructor.");
        using MainForm main = (MainForm)constructor.Invoke(new object[] { settings });
        int result = 0;
        main.Shown += async delegate {
            try {
                main.Location = new Point(100, 120);
                await RunOperation(main, delegate { return true; });
                Require(main.Visible && main.Location == new Point(100, 120), "No-drag transfer changed its owner's position.");
                Console.WriteLine("PASS: no-drag transfer restores its owner position.");
                using ManualResetEventSlim moved = new ManualResetEventSlim();
                using Majorsilence.Forms.Timer timer = new Majorsilence.Forms.Timer();
                timer.Interval = 10;
                timer.Tick += delegate {
                    MnxDialog? child = main.OwnedForms.OfType<MnxDialog>().FirstOrDefault(dialog => dialog.Visible);
                    if (child is null) {
                        return;
                        }
                    timer.Stop();
                    MethodInfo start = typeof(MWindow).GetMethod("BeginTitleBarDrag", BindingFlags.Instance | BindingFlags.NonPublic)!;
                    MethodInfo end = typeof(MWindow).GetMethod("EndTitleBarDrag", BindingFlags.Instance | BindingFlags.NonPublic)!;
                    start.Invoke(child, null);
                    child.Location = new Point(child.Left + 37, child.Top + 29);
                    end.Invoke(child, null);
                    moved.Set();
                    };
                timer.Start();
                await RunOperation(main, delegate {
                    Require(moved.Wait(TimeSpan.FromSeconds(5)), "The transfer dialog did not receive the test gesture.");
                    return true;
                    });
                Require(main.Visible && main.Location == new Point(137, 149),
                    "Transfer restoration erased the owner's caption-drag delta: " + main.Location);
                Console.WriteLine("PASS: transfer restoration preserves the explicit progress-title drag delta.");
                }
            catch (Exception error) {
                result = 1;
                Console.WriteLine("FAIL: " + error);
                }
            finally {
                main.Close();
                }
            };
        Application.Run(main);
        return result;
        }

    private static Task<bool> RunOperation(MainForm main, Func<bool> operation) {
        MethodInfo method = typeof(MainForm).GetMethod("RunOperation", BindingFlags.Instance | BindingFlags.NonPublic)!
            .MakeGenericMethod(typeof(bool));
        object?[] arguments = method.GetParameters().Select(parameter => parameter.DefaultValue).ToArray();
        arguments[0] = "Checking transfer owner movement";
        arguments[1] = operation;
        return (Task<bool>)method.Invoke(main, arguments)!;
        }

    private static void Require(bool condition, string message) {
        if (!condition) {
            throw new InvalidOperationException(message);
            }
        }
    }
