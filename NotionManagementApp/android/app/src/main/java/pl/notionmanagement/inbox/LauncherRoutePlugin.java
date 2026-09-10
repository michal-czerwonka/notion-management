package pl.notionmanagement.inbox;

import android.content.Intent;

import com.getcapacitor.JSObject;
import com.getcapacitor.Plugin;
import com.getcapacitor.annotation.CapacitorPlugin;

@CapacitorPlugin(name = "LauncherRoute")
public class LauncherRoutePlugin extends Plugin {
    @Override
    protected void handleOnNewIntent(Intent intent) {
        JSObject payload = new JSObject();
        payload.put("view", isRoutineTasksLauncher(intent) ? "routines" : "inbox");
        notifyListeners("shortcutOpen", payload, true);
    }

    private boolean isRoutineTasksLauncher(Intent intent) {
        return intent != null
            && intent.getComponent() != null
            && intent.getComponent().getClassName().endsWith(".RoutineTasksLauncher");
    }
}
