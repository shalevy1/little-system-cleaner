﻿/*
    Little System Cleaner
    Copyright (C) 2008 Little Apps (http://www.little-apps.com/)

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;
using Little_System_Cleaner.Misc;
using Little_System_Cleaner.Privacy_Cleaner.Controls;
using Little_System_Cleaner.Privacy_Cleaner.Helpers;
using Little_System_Cleaner.Privacy_Cleaner.Helpers.Results;
using Microsoft.Win32;

namespace Little_System_Cleaner.Privacy_Cleaner.Scanners
{
    public abstract class ScannerBase : INotifyPropertyChanged
    {
        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string prop)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }

        #endregion

        public static CancellationTokenSource CancellationToken;

        public ObservableCollection<ScannerBase> Children { get; } = new ObservableCollection<ScannerBase>();

        private bool? _bIsChecked = true;

        #region IsChecked Methods
        public void SetIsChecked(bool? value, bool updateChildren, bool updateParent)
        {
            if (value == _bIsChecked)
                return;

            _bIsChecked = value;

            if (updateChildren && _bIsChecked.HasValue)
            {
                foreach (ScannerBase c in Children)
                    c.SetIsChecked(_bIsChecked, true, false);
            }

            if (updateParent)
                Parent?.VerifyCheckState();

            OnPropertyChanged("IsChecked");
        }

        void VerifyCheckState()
        {
            bool? state = null;
            for (int i = 0; i < Children.Count; ++i)
            {
                bool? current = Children[i].IsChecked;
                if (i == 0)
                {
                    state = current;
                }
                else if (state != current)
                {
                    state = null;
                    break;
                }
            }
            SetIsChecked(state, false, true);
        }
        #endregion

        public bool? IsChecked
        {
            get { return _bIsChecked; }
            set { SetIsChecked(value, true, true); }
        }

        public ScannerBase Parent { get; set; }
        public string Description { get; set; }

        private bool _skipped;

        /// <summary>
        /// If true, all scanners under Parent will be skipped
        /// </summary>
        public bool Skipped
        {
            get
            {
                if (Parent != null)
                    return Parent.Skipped;
                return _skipped;
            }
            set
            {
                if (Parent != null)
                    Parent.Skipped = value;
                else
                    _skipped = value;
            }
        }

        public ImageSource bMapImg { get; private set; }
        public Bitmap Icon
        {
            set
            {
                IntPtr hBitmap = value.GetHbitmap();

                bMapImg = Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

                OnPropertyChanged("bMapImg");
            }
        }

        public virtual void Scan(ScannerBase child)
        {
        }

        public virtual void Scan()
        {
        }

        public virtual bool IsRunning()
        {
            return false;
        }


        private string _errors;

        public string Errors
        {
            get
            {
                return _errors;
            }
            set
            {
                if (Parent != null)
                    Parent.Errors = value;
                else
                {
                    _errors = value;
                    OnPropertyChanged("Errors");
                }
                    
            }
        }

        /// <summary>
        /// Returns process name for scanner
        /// </summary>
        public virtual string ProcessName => string.Empty;

        private string _name;

        public string Name
        {
            get { return _name; }
            set 
            {
                _name = value;
                Results = new RootNode(Section);
            }
        }

        public string ToolTipText => Description;

        public string PluginPath
        {
            get;
            set;
        }

        public string Section
        {
            get
            {
                if (Parent == null)
                {
                    return Name;
                }
                string parentName = Parent.Name;
                return parentName + " - " + Name;
            }
        }

        private string _status;

        public string Status
        {
            get { return _status; }
            set 
            {
                if (Parent != null)
                    Parent.Status = value;
                else
                {
                    _status = value;
                    OnPropertyChanged("Status");
                }
                    
            }
        }

        private string _image;

        public string Image
        {
            get { return _image; }
            set
            {
                if (Parent != null)
                    Parent.Image = value;
                else
                {
                    _image = value;
                    OnPropertyChanged("Image");
                }
            }
        }

        private string _animatedImage;

        public string AnimatedImage
        {
            get { return _animatedImage; }
            set
            {
                if (Parent != null)
                    Parent.AnimatedImage = value;
                else
                {
                    _animatedImage = value;
                    OnPropertyChanged("AnimatedImage");
                }
            }
        }

        public void LoadGif()
        {
            if (Parent != null)
            {
                Parent.LoadGif();
                return;
            }

            AnimatedImage = "/Little_System_Cleaner;component/Resources/ajax-loader.gif";
        }

        public void UnloadGif()
        {
            if (Parent != null)
            {
                Parent.UnloadGif();
                return;
            }

            AnimatedImage = null;
            Image = @"/Little_System_Cleaner;component/Resources/registry cleaner/finished-scanning.png";
        }

        public ResultNode Results;

        #region Plugin Scanner

        /// <summary>
        /// Ensures that plugin is valid before being added
        /// </summary>
        /// <param name="xmlFilePath">Path to plugin XML file</param>
        /// <param name="name">Outputs name of plugin</param>
        /// <param name="description">Outputs description of plugin</param>
        /// <returns>True if plugin is valid</returns>
        public bool PluginIsValid(string xmlFilePath, out string name, out string description)
        {
            bool bRet = false;
            name = description = string.Empty;

            if (!File.Exists(xmlFilePath))
                return false;

            using (XmlTextReader xmlReader = new XmlTextReader(xmlFilePath))
            {
                // Read Information node and add it to node list
                if (xmlReader.ReadToFollowing("Information"))
                {
                    if (xmlReader.ReadToFollowing("Name"))
                        name = xmlReader.ReadElementContentAsString();
                    if (xmlReader.ReadToFollowing("Description"))
                        description = xmlReader.ReadElementContentAsString();

                    if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(description))
                        return false;
                }

                // See if scanner is valid
                if (xmlReader.ReadToFollowing("IsValid"))
                {
                    while (xmlReader.Read())
                    {
                        if (xmlReader.NodeType != XmlNodeType.Element)
                            continue;

                        // Parse registry key and see if it exists
                        switch (xmlReader.Name)
                        {
                            case "KeyExist":
                            {
                                string regKeyPath = xmlReader.ReadElementContentAsString();

                                bRet = !string.IsNullOrWhiteSpace(regKeyPath) && Utils.RegKeyExists(regKeyPath);

                                if (!bRet)
                                    return false;
                            }
                                break;
                            case "ValueExist":
                            {
                                string regKeyPath = xmlReader.GetAttribute("RegKey");
                                string valueNameRegEx = xmlReader.GetAttribute("ValueName");

                                if (string.IsNullOrWhiteSpace(regKeyPath) || string.IsNullOrWhiteSpace(valueNameRegEx))
                                    bRet = false;
                                else
                                {
                                    using (RegistryKey rk = Utils.RegOpenKey(regKeyPath))
                                    {
                                        if (rk == null)
                                            continue;

                                        string[] valueNames = null;

                                        try
                                        {
                                            valueNames = rk.GetValueNames();
                                        }
                                        catch (SecurityException ex)
                                        {
                                            Debug.WriteLine("The following exception occurred: " + ex.Message + "\nUnable to get registry key (" + regKeyPath + ") value names.");
                                            bRet = false;
                                        }
                                        catch (UnauthorizedAccessException ex)
                                        {
                                            Debug.WriteLine("The following exception occurred: " + ex.Message + "\nUnable to get registry key (" + regKeyPath + ") value names.");
                                            bRet = false;
                                        }

                                        if (valueNames != null)
                                        {
                                            if (valueNames.Where(valueName => !string.IsNullOrWhiteSpace(valueName)).Any(valueName => Regex.IsMatch(valueName, valueNameRegEx)))
                                                bRet = true;
                                        }
                                    }
                                }

                                if (!bRet)
                                    return false;
                            }
                                break;
                            case "FileExist":
                                string filePath = xmlReader.ReadElementContentAsString();

                                if (string.IsNullOrWhiteSpace(filePath))
                                    bRet = false;
                                else
                                {
                                    filePath = MiscFunctions.ExpandVars(filePath);
                                    bRet = File.Exists(filePath);
                                }
                            
                                if (!bRet)
                                    return false;
                                break;
                            case "FolderExist":
                                string folderPath = xmlReader.ReadElementContentAsString();

                                if (string.IsNullOrWhiteSpace(folderPath))
                                    bRet = false;
                                else
                                {
                                    folderPath = MiscFunctions.ExpandVars(folderPath);
                                    bRet = Directory.Exists(folderPath);
                                }

                                if (!bRet)
                                    return false;
                                break;
                        }
                    }
                }

                // Ensure IsRunning commands are valid before being added
                while (xmlReader.ReadToFollowing("IsRunning"))
                {
                    if (string.IsNullOrWhiteSpace(xmlReader.ReadElementContentAsString()))
                    {
                        bRet = false;
                        break;
                    }
                }

                // Ensure Action commands are valid before being added
                if (!xmlReader.ReadToFollowing("Action"))
                    return bRet;

                while (xmlReader.Read())
                {
                    if (xmlReader.NodeType != XmlNodeType.Element)
                        continue;

                    if (xmlReader.Name == "DeleteKey")
                    {
                        string regPath = xmlReader.ReadElementContentAsString();

                        if (string.IsNullOrWhiteSpace(regPath))
                        {
                            bRet = false;
                            break;
                        }
                    }

                    if (xmlReader.Name == "DeleteValue")
                    {
                        string regPath = xmlReader.GetAttribute("RegKey");
                        string valueNameRegEx = xmlReader.GetAttribute("ValueName");

                        if (string.IsNullOrWhiteSpace(regPath) || string.IsNullOrWhiteSpace(valueNameRegEx))
                        {
                            bRet = false;
                            break;
                        }
                    }

                    if (xmlReader.Name == "DeleteFile")
                    {
                        string filePath = xmlReader.ReadElementContentAsString();

                        if (string.IsNullOrWhiteSpace(filePath))
                        {
                            bRet = false;
                            break;
                        }
                    }

                    if (xmlReader.Name == "DeleteFolder")
                    {
                        string folderPath = xmlReader.ReadElementContentAsString();

                        if (string.IsNullOrWhiteSpace(folderPath))
                        {
                            bRet = false;
                            break;
                        }
                    }

                    if (xmlReader.Name == "DeleteFileList")
                    {
                        string searchPath = xmlReader.GetAttribute("Path");
                        string searchText = xmlReader.GetAttribute("SearchText");
                        if (string.IsNullOrWhiteSpace(searchPath) || string.IsNullOrWhiteSpace(searchText))
                        {
                            bRet = false;
                            break;
                        }
                    }

                    if (xmlReader.Name == "DeleteFolderList")
                    {
                        string searchPath = xmlReader.GetAttribute("Path");
                        string searchText = xmlReader.GetAttribute("SearchText");

                        if (string.IsNullOrWhiteSpace(searchPath) || string.IsNullOrWhiteSpace(searchText))
                        {
                            bRet = false;
                            break;
                        }
                    }

                    if (xmlReader.Name == "FindRegKey")
                    {
                        string regKey = xmlReader.GetAttribute("RegKey");

                        if (string.IsNullOrWhiteSpace(regKey))
                        {
                            bRet = false;
                            break;
                        }

                        // Must have child nodes
                        using (XmlReader children = xmlReader.ReadSubtree()) {
                            if (children.IsEmptyElement)
                            {
                                bRet = false;
                                break;
                            }

                            bool hasChildren = false;
                            while (children.Read()) {
                                if ((children.Name == "IfSubKey" || children.Name == "IfValueName") && !string.IsNullOrWhiteSpace(xmlReader.GetAttribute("SearchText")))
                                {
                                    hasChildren = true;
                                    break;
                                }
                            }

                            if (!hasChildren)
                            {
                                bRet = false;
                                break;
                            }
                        }
                    }

                    if (xmlReader.Name == "FindPath")
                    {
                        string searchPath = xmlReader.GetAttribute("Path");
                        string searchText = xmlReader.GetAttribute("SearchText");

                        if (string.IsNullOrWhiteSpace(searchPath) || string.IsNullOrWhiteSpace(searchText))
                        {
                            bRet = false;
                            break;
                        }

                        // Must have child nodes
                        using (XmlReader children = xmlReader.ReadSubtree())
                        {
                            if (children.IsEmptyElement)
                            {
                                bRet = false;
                                break;
                            }

                            bool hasChildren = false;
                            while (children.Read())
                            {
                                if ((children.Name == "IfFile" || children.Name == "IfFile") && !string.IsNullOrWhiteSpace(xmlReader.GetAttribute("SearchText")))
                                {
                                    hasChildren = true;
                                    break;
                                }
                            }

                            if (!hasChildren)
                            {
                                bRet = false;
                                break;
                            }
                        }
                    }

                    if (xmlReader.Name == "RemoveINIValue")
                    {
                        string filePath = xmlReader.GetAttribute("Path");
                        string sectionRegEx = xmlReader.GetAttribute("Section");
                        string valueRegEx = xmlReader.GetAttribute("Name");

                        if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(sectionRegEx) || string.IsNullOrWhiteSpace(valueRegEx))
                        {
                            bRet = false;
                            break;
                        }
                    }

                    if (xmlReader.Name == "RemoveINISection")
                    {
                        string filePath = xmlReader.GetAttribute("Path");
                        string sectionRegEx = xmlReader.GetAttribute("Section");

                        if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(sectionRegEx))
                        {
                            bRet = false;
                            break;
                        }
                    }

                    if (xmlReader.Name == "RemoveXML")
                    {
                        string filePath = xmlReader.GetAttribute("Path");
                        string xPath = xmlReader.GetAttribute("XPath");

                        if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(xPath))
                        {
                            bRet = false;
                            break;
                        }
                    }
                }
            }

            return bRet;
        }

        /// <summary>
        /// Goes through the nodes and parses the .xml file
        /// </summary>
        public void ScanPlugins()
        {
            foreach (ScannerBase n in Children)
            {
                if (CancellationToken.IsCancellationRequested)
                    break;

                if (!string.IsNullOrEmpty(n.Name) && !string.IsNullOrEmpty(n.PluginPath))
                    ScanPlugin(n.Name, n.PluginPath);
            }
        }

        /// <summary>
        /// Parses the .xml file and performs the actions specified
        /// </summary>
        /// <param name="name">Scanner name</param>
        /// <param name="pluginFile">Path to .xml file</param>
        public void ScanPlugin(string name, string pluginFile)
        {
            if (!File.Exists(pluginFile))
                return;

            PluginFunctions pluginFunctions = new PluginFunctions();

            try
            {
                using (XmlReader xmlReader = XmlReader.Create(pluginFile))
                {
                    while (xmlReader.ReadToFollowing("IsRunning"))
                    {
                        if (CancellationToken.IsCancellationRequested)
                            return;

                        string procName = xmlReader.ReadElementContentAsString();

                        if (RunningMsg.DisplayRunningMsg(name, procName).GetValueOrDefault() == false)
                            return;
                    }
                }

                using (XmlReader xmlReader = XmlReader.Create(pluginFile))
                {
                    if (xmlReader.ReadToFollowing("Action"))
                    {
                        while (xmlReader.Read())
                        {
                            if (CancellationToken.IsCancellationRequested)
                                return;

                            if (xmlReader.NodeType != XmlNodeType.Element)
                                continue;

                            if (xmlReader.Name == "DeleteKey")
                            {
                                string regPath = xmlReader.ReadElementContentAsString();
                                RegistryKey regKey = Utils.RegOpenKey(regPath);
                                bool recurse = ((xmlReader.GetAttribute("Recursive") == "Y"));

                                pluginFunctions.DeleteKey(regKey, recurse);
                            }

                            if (xmlReader.Name == "DeleteValue")
                            {
                                string regPath = xmlReader.GetAttribute("RegKey");
                                string valueNameRegEx = xmlReader.GetAttribute("ValueName");

                                RegistryKey regKey = Utils.RegOpenKey(regPath, false);

                                pluginFunctions.DeleteValue(regKey, valueNameRegEx);
                            }

                            if (xmlReader.Name == "DeleteFile")
                            {
                                string filePath = MiscFunctions.ExpandVars(xmlReader.ReadElementContentAsString());

                                pluginFunctions.DeleteFile(filePath);
                            }

                            if (xmlReader.Name == "DeleteFolder")
                            {
                                string folderPath = MiscFunctions.ExpandVars(xmlReader.ReadElementContentAsString());
                                bool recurse = (xmlReader.GetAttribute("Recursive") == "Y");

                                pluginFunctions.DeleteFolder(folderPath, recurse);
                            }

                            if (xmlReader.Name == "DeleteFileList")
                            {
                                string searchPath = MiscFunctions.ExpandVars(xmlReader.GetAttribute("Path"));
                                string searchText = xmlReader.GetAttribute("SearchText");
                                SearchOption includeSubFolders = ((xmlReader.GetAttribute("IncludeSubFolders") == "Y") ? (SearchOption.AllDirectories) : (SearchOption.TopDirectoryOnly));

                                pluginFunctions.DeleteFileList(searchPath, searchText, includeSubFolders);
                            }

                            if (xmlReader.Name == "DeleteFolderList")
                            {
                                string searchPath = MiscFunctions.ExpandVars(xmlReader.GetAttribute("Path"));
                                string searchText = xmlReader.GetAttribute("SearchText");
                                SearchOption includeSubFolders = ((xmlReader.GetAttribute("IncludeSubFolders") == "Y") ? (SearchOption.AllDirectories) : (SearchOption.TopDirectoryOnly));

                                pluginFunctions.DeleteFolderList(searchPath, searchText, includeSubFolders);
                            }

                            if (xmlReader.Name == "FindRegKey")
                            {
                                string regKey = xmlReader.GetAttribute("RegKey");
                                bool includeSubKeys = ((xmlReader.GetAttribute("IncludeSubKeys") == "Y"));

                                RegistryKey rk = Utils.RegOpenKey(regKey, false);
                                XmlReader xmlChildren = xmlReader.ReadSubtree();

                                pluginFunctions.DeleteFoundRegKeys(rk, includeSubKeys, xmlChildren);
                            }

                            if (xmlReader.Name == "FindPath")
                            {
                                string searchPath = MiscFunctions.ExpandVars(xmlReader.GetAttribute("Path"));
                                string searchText = xmlReader.GetAttribute("SearchText");
                                SearchOption includeSubFolders = ((xmlReader.GetAttribute("IncludeSubFolders") == "Y") ? (SearchOption.AllDirectories) : (SearchOption.TopDirectoryOnly));

                                XmlReader xmlChildren = xmlReader.ReadSubtree();

                                pluginFunctions.DeleteFoundPaths(searchPath, searchText, includeSubFolders, xmlChildren);
                            }

                            if (xmlReader.Name == "RemoveINIValue")
                            {
                                string filePath = MiscFunctions.ExpandVars(xmlReader.GetAttribute("Path"));
                                string sectionRegEx = xmlReader.GetAttribute("Section");
                                string valueRegEx = xmlReader.GetAttribute("Name");

                                pluginFunctions.DeleteIniValue(filePath, sectionRegEx, valueRegEx);
                            }

                            if (xmlReader.Name == "RemoveINISection")
                            {
                                string filePath = MiscFunctions.ExpandVars(xmlReader.GetAttribute("Path"));
                                string sectionRegEx = xmlReader.GetAttribute("Section");

                                pluginFunctions.DeleteIniSection(filePath, sectionRegEx);
                            }

                            if (xmlReader.Name == "RemoveXML")
                            {
                                string filePath = MiscFunctions.ExpandVars(xmlReader.GetAttribute("Path"));
                                string xPath = xmlReader.GetAttribute("XPath");

                                pluginFunctions.DeleteXml(filePath, xPath);
                            }
                        }
                    }
                }
            }
            catch (SecurityException ex)
            {
                Debug.WriteLine("The following error occurred: {0}\nUnable to load plugin file ({1})", ex.Message, pluginFile);
            }
            catch (UriFormatException ex)
            {
                Debug.WriteLine("The following error occurred: {0}\nUnable to load plugin file ({1})", ex.Message, pluginFile);
            }


            if (pluginFunctions.RegistrySubKeys.Count > 0)
                Wizard.StoreBadRegKeySubKeys(name, pluginFunctions.RegistrySubKeys);

            if (pluginFunctions.RegistryValueNames.Count > 0)
                Wizard.StoreBadRegKeyValueNames(name, pluginFunctions.RegistryValueNames);

            if (pluginFunctions.FilePaths.Count > 0)
                Wizard.StoreBadFileList(name, pluginFunctions.FilePaths.ToArray());

            if (pluginFunctions.Folders.Count > 0)
                Wizard.StoreBadFolderList(name, pluginFunctions.Folders);

            if (pluginFunctions.IniList.Count > 0)
                Wizard.StoreIniKeys(name, pluginFunctions.IniList.ToArray());

            if (pluginFunctions.XmlPaths.Count > 0)
                Wizard.StoreXml(name, pluginFunctions.XmlPaths);
        }

        
        #endregion
    }
}
