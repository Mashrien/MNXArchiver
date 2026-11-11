namespace Mash.GUI.MNXArchiver.Backends;

public sealed class BackendCommandLine {
    public GuiBackend Preference { get; private set; }
    public bool HasOverride { get; private set; }
    public string[] Arguments { get; private set; } = Array.Empty<string>();
    public static BackendCommandLine Parse(string[] arguments) {
        BackendCommandLine result = new BackendCommandLine();
        List<string> forwarded = new List<string>();
        for (int index = 0; index < arguments.Length; index++) {
            string argument = arguments[index];
            if (argument == "--") {
                forwarded.AddRange(arguments.Skip(index));
                break;
                }
            string? value = null;
            if (argument == "--backend") {
                index++;
                if (index >= arguments.Length) {
                    throw new ArgumentException("--backend requires a value: auto, mwin or gtk3.");
                    }
                value = arguments[index];
                }
            else if (argument.StartsWith("--backend=", StringComparison.Ordinal)) {
                value = argument.Substring("--backend=".Length);
                }
            if (value is null) {
                forwarded.Add(argument);
                continue;
                }
            if (result.HasOverride) {
                throw new ArgumentException("Specify --backend only once.");
                }
            if (!value.Equals("auto", StringComparison.OrdinalIgnoreCase)
                && !value.Equals("mwin", StringComparison.OrdinalIgnoreCase)
                && !value.Equals("gtk4", StringComparison.OrdinalIgnoreCase)
                && !value.Equals("gtk3", StringComparison.OrdinalIgnoreCase)) {
                throw new ArgumentException("Unknown backend '" + value + "'. Use auto, mwin or gtk3.");
                }
            result.HasOverride = true;
            result.Preference = BackendSelection.ParsePreference(value);
            }
        result.Arguments = forwarded.ToArray();
        return result;
        }
    }
