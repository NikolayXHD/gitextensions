GitExtensions stores themes in *.css files.

Preinstalled themes reside in `%programfiles(x86)%\GitExtensions\Themes`.

Users are free to create and use their own themes. The recommended directory for user-defined theme is
`%AppData%\Roaming\GitExtensions\GitExtensions\Themes`, because it is not erased when upgrading GitExtensions.

For each file found in preinstalled themes directory or user-defined themes directory, ColorSettings page will show an
entry in Theme drop down list.

The portable version of GitExtensions only looks for themes in \Themes subdirectory.

To create a theme based on existing one and change a few colors use .css import directive.

- To import from a preinstalled theme:
```css
@import url("dark.css");
```

- To import from a user-defined theme:
```css
@import url("{UserAppData}/dark.css");
```
