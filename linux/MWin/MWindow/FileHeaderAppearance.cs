using System.Drawing;
using Majorsilence.Forms;
using Majorsilence.Forms.Drawing;

namespace MashWin;

internal static class FileHeaderAppearance {
    internal static void Apply(ListView list, Color background, Color highlight, Font font, Color text) {
        list.SetHeaderAppearance(background, highlight, font, text);
    }
}
