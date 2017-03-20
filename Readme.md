Borderless Windowing Utility
----------------------------

A lot of games, for whatever reason, don't have a borderless windowed mode. This means that for people with multiple monitors there's a choice between playing in windowed mode or else dealing with Windows having a full on stroke and minimizing the game every time you try to do something on the second monitor.

This simple utility displays a list of open windows and allows you to select one and attempt to coerce it into borderless windowed mode, then resize it to fill the screen.

The effect (when it works) is that you can play fullscreen without the crazy behavior from Windows when you do something offscreen. It also allows you to alt-tab more freely, regardless of whether you're using multiple screens or not, since the game is technically still windowed.

If you just want to use the tool then you can download a build here: https://github.com/SimpleRepos/Borderless-Windowing-Utility/releases/tag/1.1

This tool is written in C# because it has the really nice WinForms building tools in VS, but most of the functionality I needed is exposed via WinAPI (at least that's where I know it from) so there's a big chunk of pinvoke surrounded by a pretty small project. I added some commenting, so it shouldn't be too hard to follow along if you're familiar with WinForms and/or WinAPI.

Please feel free to file issues if you run into any problems or have questions about the code.
