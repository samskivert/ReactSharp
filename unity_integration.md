Unity Integration
=================

These instructions are for Unity 2017.3, which introduced the "assembly definition files" feature described here (https://docs.unity3d.com/Manual/ScriptCompilationAssemblyDefinitionFiles.html).

1. Add a ReactSharp git submodule to your Unity project. This needs to live somewhere *inside* your Assets folder in order for Unity to see it. I've created an Assets/ThirdParty folder for this purpose: `git submodule add <ReactSharpRepoURL> Assets/ThirdParty/ReactSharp`. (I'm using git submodules because Unity doesn't seem to have a good story for how to handle third-party dependencies otherwise.)
2. Open your game project in the Unity editor. It will notice the presence of the new library and generate a bunch of annoying .meta files.
3. Create an assembly definition file ('asmdef') for your game code. It should live in the Assets/Scripts folder, or whatever you've set as the root folder for your code. Call this whatever you want: "GameCode" or the name of your game, for example.
4. Add a reference to ReactSharp's asmdef (included in the ReactSharp repo) to your newly-created GameCode asmdef. You can do this in the Unity editor, or just edit the GameCode.asmdef file directly - it's just a json. It should look something like this:
    ```
    {
        "name": "GameCode",
        "references": [
            "ReactSharp"
        ],
        "includePlatforms": [],
        "excludePlatforms": []
    }
    ```
5. In the Unity editor, use the "Assets > Open C# Project" menu item to regenerate the project's .sln file and open it in Visual Studio or MonoDevelop or Rider or whatever. The reason for all the asmdef nonsense is that Unity rebuilds the .sln whenever there are changes to the Assets folder, blowing away any changes you may have made in your IDE.
6. You'll notice that Unity has autogenerated two new csproj's inside the solution: GameCode and ReactSharp. Each asmdef results in the creation of a new top-level csproj. (You cannot simply add ReactSharp's own csproj to your solution, because of the aforementioned Unity sln-auto-regeneration business.) These autogenerated csproj files live alongside the project's sln file, outside of the Assets folder.