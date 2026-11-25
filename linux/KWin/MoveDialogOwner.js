// Bound once, on the dialog's first title press. KWin supplies both surfaces
// and their positions in the same logical-coordinate space on every monitor.
function unloadMovement() {
    callDBus("org.kde.KWin", "/Scripting", "org.kde.kwin.Scripting", "unloadScript", mnxScriptName);
    }

function attachOwnerMovement() {
    const dialog = workspace.activeWindow;
    if (!dialog || dialog.pid !== mnxPid) {
        unloadMovement();
        return;
        }
    // The active surface must be the actual MWin dialog receiving the press.
    if (dialog.caption !== mnxTitle && dialog.captionNormal !== mnxTitle) {
        unloadMovement();
        return;
        }
    const owner = dialog.transientFor;
    if (!owner || owner.pid !== mnxPid) {
        unloadMovement();
        return;
        }

    let offset = null;
    function beginMove() {
        if (dialog.move && owner.moveable && !owner.fullScreen) {
            offset = {
                x: owner.frameGeometry.x - dialog.frameGeometry.x,
                y: owner.frameGeometry.y - dialog.frameGeometry.y
                };
            }
        }
    function follow(geometry) {
        if (offset === null) {
            return;
            }
        const bounds = owner.frameGeometry;
        const x = geometry.x + offset.x;
        const y = geometry.y + offset.y;
        if (x !== bounds.x || y !== bounds.y) {
            // KWin exposes a read-only QRect snapshot; assign a complete rectangle.
            owner.frameGeometry = { x: x, y: y, width: bounds.width, height: bounds.height };
            }
        }
    dialog.interactiveMoveResizeStarted.connect(beginMove);
    dialog.interactiveMoveResizeStepped.connect(follow);
    dialog.interactiveMoveResizeFinished.connect(function () {
        follow(dialog.frameGeometry);
        offset = null;
        });
    workspace.windowRemoved.connect(function (removed) {
        if (removed === dialog || removed === owner) {
            unloadMovement();
            }
        });
    }
attachOwnerMovement();
