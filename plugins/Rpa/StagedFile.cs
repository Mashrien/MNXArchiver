namespace Mash.UI.MNXFile.GUI.Plugins.Rpa;

internal sealed class StagedFile(string destination) : IDisposable {
    private bool created;

    internal string Destination { get; } = destination;
    internal string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(destination)!,
        $".mnx-rpa-{Guid.NewGuid():N}.tmp");

    internal FileStream Open() {
        RpaPaths.CheckOutput(Path, overwrite: false);
        FileStream stream = new(Path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        created = true;
        return stream;
        }

    internal void Commit(bool overwrite) {
        RpaPaths.RequireFile(Path);
        RpaPaths.CheckOutput(Destination, overwrite);
        File.Move(Path, Destination, overwrite);
        created = false;
        }

    public void Dispose() {
        if (!created)
            return;
        try {
            RpaPaths.RequireFile(Path);
            File.Delete(Path);
            }
        catch (IOException) { }
        catch (InvalidDataException) { }
        catch (UnauthorizedAccessException) { }
        }
    }
