#!/usr/bin/env python3
"""Writes a WiX source file listing every file in the staged app folder.
Usage: make-wxs.py <staged-folder> <version> <manufacturer> <icon.ico> > TempWidget.wxs"""
import os, sys, uuid
from xml.sax.saxutils import quoteattr

folder, version, manufacturer, icon = sys.argv[1:5]
# Never change this: it is how Windows recognises a newer TempWidget as an upgrade of an older one.
UPGRADE_CODE = "7B0C6E0A-52C1-4B0E-9B55-6B1F5E2B9A41"
ns = uuid.UUID("3f0f6d0e-8f0b-4d0a-a8a5-0d6f1f1a2b3c")

files = sorted(f for f in os.listdir(folder) if os.path.isfile(os.path.join(folder, f)))
components, refs = [], []
for i, name in enumerate(files):
    cid = "C%d" % i
    guid = str(uuid.uuid5(ns, name.lower())).upper()   # stable per file name across versions
    src = quoteattr(os.path.join(folder, name))
    shortcut = ""
    if name.lower() == "tempwidget.exe":
        shortcut = ('\n            <Shortcut Id="StartMenuShortcut" Directory="ProgramMenuFolder" Name="TempWidget" '
                    'Description="Temperatures, fans and other sensors" WorkingDirectory="INSTALLFOLDER" '
                    'Icon="AppIcon.exe" IconIndex="0" Advertise="yes" />\n          ')
    components.append('        <Component Id="%s" Guid="%s" Win64="yes">\n          <File Id="F%d" Name=%s Source=%s KeyPath="yes">%s</File>\n        </Component>'
                      % (cid, guid, i, quoteattr(name), src, shortcut))
    refs.append('      <ComponentRef Id="%s" />' % cid)

print('''<?xml version="1.0" encoding="utf-8"?>
<Wix xmlns="http://schemas.microsoft.com/wix/2006/wi">
  <Product Id="*" Name="TempWidget" Language="1033" Version="%s" Manufacturer=%s UpgradeCode="%s">
    <Package InstallerVersion="500" Compressed="yes" InstallScope="perMachine"
             Description="TempWidget %s" Comments="Desktop widget for temperatures, fans and other sensors" />
    <MajorUpgrade DowngradeErrorMessage="A newer version of TempWidget is already installed." />
    <Media Id="1" Cabinet="app.cab" EmbedCab="yes" />

    <Icon Id="AppIcon.exe" SourceFile=%s />
    <Property Id="ARPPRODUCTICON" Value="AppIcon.exe" />
    <Property Id="ARPNOMODIFY" Value="1" />

    <Directory Id="TARGETDIR" Name="SourceDir">
      <Directory Id="ProgramFiles64Folder">
        <Directory Id="INSTALLFOLDER" Name="TempWidget">
%s
        </Directory>
      </Directory>
      <Directory Id="ProgramMenuFolder" />
    </Directory>

    <Feature Id="Main" Title="TempWidget" Level="1">
%s
    </Feature>

    <!-- Close a running copy so its files can be replaced, and remove the startup task on uninstall. -->
    <CustomAction Id="SetCmd" Property="CMDEXE" Value="[System64Folder]cmd.exe" />
    <CustomAction Id="CloseApp" Property="CMDEXE" ExeCommand="/c taskkill.exe /IM TempWidget.exe /F"
                  Execute="deferred" Impersonate="no" Return="ignore" />
    <CustomAction Id="RemoveStartupTask" Property="CMDEXE" ExeCommand="/c schtasks.exe /Delete /TN TempWidget /F"
                  Execute="deferred" Impersonate="no" Return="ignore" />
    <InstallExecuteSequence>
      <Custom Action="SetCmd" Before="InstallValidate" />
      <!-- build.sh renumbers these so RemoveExistingProducts directly follows InstallInitialize. -->
      <Custom Action="CloseApp" After="InstallInitialize" />
      <Custom Action="RemoveStartupTask" After="CloseApp">REMOVE="ALL" AND NOT UPGRADINGPRODUCTCODE</Custom>
    </InstallExecuteSequence>
  </Product>
</Wix>''' % (version, quoteattr(manufacturer), UPGRADE_CODE, version, quoteattr(icon), "\n".join(components), "\n".join(refs)))
