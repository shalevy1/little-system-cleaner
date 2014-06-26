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

using Little_System_Cleaner.Registry_Optimizer.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Little_System_Cleaner.Registry_Optimizer.Helpers
{
    internal static class HiveManager 
    {
        /// <summary>
        /// Gets a temporary path for a registry hive
        /// </summary>
        internal static string GetTempHivePath()
        {
            try
            {
                string tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

                // File cant exists, keep retrying until we get a file that doesnt exist
                if (File.Exists(tempPath))
                    return GetTempHivePath();

                return tempPath;
            }
            catch (IOException)
            {
                return GetTempHivePath();
            }
        }

        /// <summary>
        /// Converts \\Device\\HarddiskVolumeX\... to X:\...
        /// </summary>
        /// <param name="DevicePath">Device name with path</param>
        /// <returns>Drive path</returns>
        internal static string ConvertDeviceToMSDOSName(string DevicePath)
        {
            string strDevicePath = string.Copy(DevicePath.ToLower());
            string strRetVal = "";

            // Convert \Device\HarddiskVolumeX\... to X:\...
            foreach (KeyValuePair<string, string> kvp in QueryDosDevice())
            {
                string strDrivePath = kvp.Key.ToLower();
                string strDeviceName = kvp.Value.ToLower();

                if (strDevicePath.StartsWith(strDeviceName))
                {
                    strRetVal = strDevicePath.Replace(strDeviceName, strDrivePath);
                    break;
                }
            }

            return strRetVal;
        }

        private static Dictionary<string, string> QueryDosDevice()
        {
            Dictionary<string, string> ret = new Dictionary<string, string>();

            foreach (DriveInfo di in DriveInfo.GetDrives())
            {
                if (di.IsReady)
                {
                    string strDrivePath = di.Name.Substring(0, 2);
                    StringBuilder strDeviceName = new StringBuilder(260);

                    // Convert C: to \Device\HarddiskVolume1
                    if (PInvoke.QueryDosDevice(strDrivePath, strDeviceName, 260) > 0)
                        ret.Add(strDrivePath, strDeviceName.ToString());
                }
            }

            return ret;
        }

        /// <summary>
        /// Gets the old size of the registry hives
        /// </summary>
        /// <returns>Registry size (in bytes)</returns>
        internal static long GetOldRegistrySize()
        {
            if (Wizard.RegistryHives == null)
                return 0;

            if (Wizard.RegistryHives.Count == 0)
                return 0;

            long size = 0;

            foreach (Hive h in Wizard.RegistryHives)
            {
                size += h.OldHiveSize;
            }

            return size;
        }

        /// <summary>
        /// Gets the new size of the registry hives
        /// </summary>
        /// <returns>Registry size (in bytes)</returns>
        internal static long GetNewRegistrySize()
        {
            if (Wizard.RegistryHives == null)
                return 0;

            if (Wizard.RegistryHives.Count == 0)
                return 0;

            long size = 0;

            foreach (Hive h in Wizard.RegistryHives)
            {
                size += h.NewHiveSize;
            }

            return size;
        }
    }

    
}
