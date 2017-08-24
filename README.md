# Loupe Agent #

## What's In This Repository ##

This is the primary closed source repository for the Loupe Agent.
The following deliverables live here:

  * Loupe Agent
  * Loupe Viewer

This repository will be open sourced and moved to GitHub once we've cleaned it up including:
1. *Refactor to clean up and reduce dependencies.*  The output of this step should work on 
  .NET full framework 4.5 and higher and Xamarin.
2. *Review .NET Core target options.*  We need to determine if we can target .NET Core 1.0 or 
  have to wait for .NET Standard 2.0 to be available.
3. *Port to .NET Core.*  This version would work with .NET Full, Xamarin, and .NET Core however 
  it may violate the sprit of .NET Core in some aspects to preserve commonality across platforms.
4. *Correct Copyright and License.*  Prepare for Open Source by selecting an appropriate OSS license,
  ensuring all dependencies are license compatible, modifying file headers and copyrights,
  and adding license information

## How To Build These Projects ##

### Loupe Agent ###

The Loupe Agent can be built with Visual Studio 2015 by opening src\Agent.sln.  It uses no commercial
components and is intended to be open sourced.

You will need the GibraltarSoftware.snk file to build these projects.  It is deliberately not in the 
repository to ensure it never leaks to a public repository since it is essential to our licensing system 
that this file is private.  Contact another developer to get a copy of the SNK or generate your own for
local use.

## Where Is The Rest Of Loupe? ##

Everything that depends on the Agent or Addin libraries exclusively has already been open sourced and is
under [Gibraltar Software on GitHub](http://github.com/GibraltarSoftware).