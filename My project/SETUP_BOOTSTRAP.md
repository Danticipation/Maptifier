# Bootstrap object – minimum setup (first time in Unity)

You only need to do **two things** to clear the red error and see the Maptifier UI. Everything else can stay as-is or be done later.

---

## Step 1: Create a Panel Settings asset (once per project)

1. In the **Project** window, go to **Assets** → **_project** (or any folder you like, e.g. **Assets/_project/UI**).
2. **Right‑click** in empty space → **Create** → **UI Toolkit** → **Panel Settings Asset**.
3. Name it something like **MaptifierPanelSettings** and press Enter.
4. Leave it where it is; you’ll use it in the next step.

---

## Step 2: Assign it (and the layout) on the Bootstrap object

1. In the **Hierarchy**, select your **Bootstrap** object.
2. In the **Inspector**, find the **UI Document** section (the one with the red exclamation).
3. **Panel Settings**
   - Click the small **circle** (⭘) next to “Panel Settings” **or** drag **MaptifierPanelSettings** from the Project window into the **Panel Settings** field.
   - It should no longer say “None”.
4. **Source Asset** (Visual Tree Asset)
   - In the Project window, go to **Assets** → **_project** → **UI** → **UXML**.
   - Drag **MainLayout** (the file **MainLayout.uxml**) into the **Source Asset** field in the Inspector.
   - It should no longer say “None”.

After that, the red warning on UI Document should go away.

---

## Step 3: Add the Main UI Controller (so buttons work)

**If the purple +, S, M, and other buttons do nothing when you click them**, the Bootstrap is missing the script that wires those clicks.

1. With **Bootstrap** still selected in the Hierarchy, scroll to the bottom of the **Inspector**.
2. Click **Add Component**.
3. Search for **Main UI Controller** (or **MainUIController**).
4. Add it.
5. In the new **Main UI Controller (Script)** section:
   - **Main Layout**: Assign **MainLayout** (same UXML as in UI Document — from **Assets/_project/UI/UXML**).
   - **Theme** (optional): Assign **MaptifierTheme** from **Assets/_project/UI/USS** if you have it.

After this, enter **Play** again. The **+** (import), **S** (solo), and **M** (mute) buttons should respond, and you should see toasts when you use them.

---

## Optional (you can skip for now)

- **App Config**  
  The bootstrapper can run with “None” here. To add one later: in Project, right‑click → **Create** → **Maptifier** → **App Config**, then assign that asset to the **App Config** field on the Bootstrap object.

- **Other UI (Settings, Onboarding)**  
  The main screen uses **MainLayout.uxml**. Other screens (Settings, Onboarding) use their own UXML and are usually on other GameObjects; you don’t need to set those up just to get the main UI running.

---

## After this

Press **Play**. You should see the Maptifier UI instead of an empty scene. If you still see a blue sky / grey ground, the wrong scene is in Build Settings: put your Boot (or this) scene first in **File → Build Settings**.
