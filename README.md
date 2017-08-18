# String2Resource
.Net developer tool to move hard code strings to resource file(s)

When we needed to upgrade our single language windows form application to multi-language we needed to search and replace all control label texts to replace them with references to the project resource file. With over 200 source files we did not want to do this by hand, but we couldn't find a tool to quickly move hard coded strings to a resource file.
This tool only moves the strings from the code, to the resource file. The string selection is by regular expressions. 

The initial version was specifically written to move hard code strings in all VB.Net source files of a project. It should work with C# source files, but that is untested at this point.
The regex string select/ignore works wel, provided you use the right expressions and the source code is not too bad.

Always run this tool on a copy of your source!

A compiled version with setup (https://github.com/lextendo/String2Resource/releases) is available.
