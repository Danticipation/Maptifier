# Running Maptifier for the First Time (Empty Sky/Ground Screen Fix)

If you open the app and only see a **default Unity view** (blue sky gradient + grey ground, no UI), the build is using the wrong scene. The app is designed to start from a **Boot** scene that contains the `AppBootstrapper` and the main UI. That scene is missing or not in the build.

---

## If "AppBootstrapper" doesn't appear in Add Component

Unity is opened from **`My project`**, but the Maptifier C# scripts live in the **parent** folder (**`Maptifier\Assets\_Project`**). Unity only sees assets inside **`My project`**, so the scripts are not in the project and **AppBootstrapper** never shows in Add Component.

**Fix: copy the script folders into the project (once).**

1. **Close Unity** (so it doesn’t lock files).
2. In File Explorer, copy these **folders** from the parent `Assets\_Project` into `My project\Assets\_project`:
   - From: `C:\My Applications\Maptifier\Assets\_Project\Scripts`  
     To: `C:\My Applications\Maptifier\My project\Assets\_project\Scripts`
   - From: `C:\My Applications\Maptifier\Assets\_Project\Shaders`  
     To: `C:\My Applications\Maptifier\My project\Assets\_project\Shaders`
   - From: `C:\My Applications\Maptifier\Assets\_Project\UI`  
     To: `C:\My Applications\Maptifier\My project\Assets\_project\UI`
   - From: `C:\My Applications\Maptifier\Assets\_Project\Tests`  
     To: `C:\My Applications\Maptifier\My project\Assets\_project\Tests`  
     (optional; only if you want tests in the project.)
   - From: `C:\My Applications\Maptifier\Assets\Plugins`  
     To: `C:\My Applications\Maptifier\My project\Assets\Plugins`  
     (required for Android display/encoder support.)
   - Copy `C:\My Applications\Maptifier\Assets\link.xml` to `C:\My Applications\Maptifier\My project\Assets\link.xml`.
3. **Reopen the project in Unity.** Wait for it to reimport and compile.
4. In the Project window you should see **Assets/_project/Scripts** (and Shaders, UI). Search for **AppBootstrapper** to confirm.
5. Then continue with **Add the app entry point** below: open your Boot scene, add an empty GameObject, **Add Component** → search **AppBootstrapper** and add it.

**Assembly reference errors (Display, Input, Layers, etc. missing):** If you see "The type or namespace name 'Display' does not exist in the namespace 'Maptifier'" and similar, the **Maptifier.Core** assembly could not reference the other assemblies (circular references). The fix is to use a **Bootstrap** assembly: `AppBootstrapper` lives in **Assets/_project/Scripts/Bootstrap** (with **Maptifier.Bootstrap.asmdef** that references Core and all other Maptifier assemblies). If your project already has this Bootstrap folder and asmdef, Unity should compile; if not, create them and move `AppBootstrapper.cs` from Core to Bootstrap.

---

## Why This Happens

- The editor build script (`Assets/_Project/Scripts/Editor/BuildScripts.cs`) expects **`Assets/_Project/Scenes/Boot.unity`**.
- That file does **not** exist in the project. The only scene present is **`My project/Assets/Scenes/SampleScene.unity`** (the default Unity template).
- If you built via **File → Build Settings** without using the custom build script, Unity used whatever scene was in the list (e.g. SampleScene) → you get the default sky/ground and no Maptifier UI.

---

## Fix in Unity

Do this in the **Unity Editor** (with the project opened from `C:\My Applications\Maptifier` or `C:\My Applications\Maptifier\My project`, depending on how you added it in Unity Hub).

### Option A: Create the Boot scene (recommended)

1. **Create the scene folder** (if it doesn’t exist):
   - In the Project window, under **Assets**, create: **Assets/_Project/Scenes**.
   - (If your project root is `My project`, the path may be **Assets/Scenes**; create **_Project/Scenes** under **Assets** if your Assets layout matches the script folder structure.)

2. **Create a new scene:**
   - **File → New Scene** (or use the default Basic/Empty template).
   - **File → Save As** and save it as **`Boot`** in **Assets/_Project/Scenes/** (e.g. `Assets/_Project/Scenes/Boot.unity`).

3. **Add the app entry point:**
   - In the Hierarchy, create an empty GameObject (right‑click → Create Empty). Name it e.g. **`App`** or **`Bootstrapper`**.
   - With that object selected, in the Inspector click **Add Component** and add **`AppBootstrapper`** (Maptifier.Core).
   - If the component has an **App Config** field, assign your **AppConfig** asset (create one from the project if needed: right‑click in Project → Create → Maptifier or similar, or use an existing one).

4. **Add the rest of the app:**
   - Add your main **Canvas**, **UI**, **Camera**, and any other objects that make up the Maptifier interface. If you have prefabs or another reference scene, duplicate or merge that content into **Boot** so the full UI is in this scene.

5. **Set Boot as the only build scene:**
   - **File → Build Settings**.
   - Remove any other scenes from the list (e.g. SampleScene).
   - Click **Add Open Scenes** (with Boot open) so **Boot** is the only scene, and it’s at index **0**.
   - Click **Build** or **Build And Run** and test on the device again.

### Option B: Use the existing scene and add the bootstrapper

If you prefer to keep using the current scene (e.g. **SampleScene**):

1. Open that scene in Unity.
2. Add an empty GameObject and add the **AppBootstrapper** component (and **AppConfig** if required).
3. Add your main UI/Canvas to that scene if it’s not already there.
4. In **File → Build Settings**, ensure **only this scene** is in the list and at index 0.
5. Build and run again.

---

## After the Fix

- The first scene in **Build Settings** must be the one that has **AppBootstrapper** and the full Maptifier UI.
- If you use the custom build script (**BuildScripts.BuildAndroidAab**), it will use **`Assets/_Project/Scenes/Boot.unity`**, so create and save the Boot scene at that path for the script to find it.

Once the correct scene is built and run, you should see the Maptifier interface instead of the empty sky/ground view.
