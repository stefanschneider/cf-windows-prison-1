﻿namespace CloudFoundry.WindowsPrison.Utilities
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.DirectoryServices;
    using System.DirectoryServices.AccountManagement;
    using System.Globalization;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Security.Principal;
    using System.Text;

    /// <summary>
    /// This is a helper class for managing Windows Users and Groups.
    /// </summary>
    public static class WindowsUsersAndGroups
    {
        /// <summary>
        /// Gets all the existing windows users.
        /// </summary>
        /// <returns>Local users account names.</returns>
        public static HashSet<string> Users
        {
            get
            {
                HashSet<string> users = new HashSet<string>();

                using (var context = new PrincipalContext(ContextType.Machine))
                {
                    using (var userPrincipal = new UserPrincipal(context))
                    {
                        using (var searcher = new PrincipalSearcher(userPrincipal))
                        {
                            foreach (var result in searcher.FindAll())
                            {
                                DirectoryEntry de = result.GetUnderlyingObject() as DirectoryEntry;
                                users.Add(de.Name);
                            }
                        }
                    }
                }

                return users;
            }
        }

        /// <summary>
        /// Gets all the existing windows users with descriptions.
        /// </summary>
        /// <returns>Local users account names with descriptions.</returns>
        public static Dictionary<string, string> DescriptionForUsers
        {
            get
            {
                Dictionary<string, string> users = new Dictionary<string, string>();

                using (var context = new PrincipalContext(ContextType.Machine))
                {
                    using (UserPrincipal userPrincipal = new UserPrincipal(context))
                    {
                        using (var searcher = new PrincipalSearcher(userPrincipal))
                        {
                            foreach (var result in searcher.FindAll())
                            {
                                DirectoryEntry de = result.GetUnderlyingObject() as DirectoryEntry;
                                users.Add(de.Name, de.Properties["Description"].Value.ToString());
                            }
                        }
                    }
                }

                return users;
            }
        }

        /// <summary>
        /// Gets the user that the current application is running as.
        /// </summary>
        /// <returns>The username of the user.</returns>
        public static string CurrentUser
        {
            get
            {
                return System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            }
        }

        /// <summary>
        /// Creates a Windows user.
        /// </summary>
        /// <param name="userName">Name of the user.</param>
        /// <param name="password">The password.</param>
        public static void CreateUser(string userName, string password)
        {
            CreateUser(userName, password, string.Format(CultureInfo.InvariantCulture, "User '{0}' was created by the Windows Prison.", userName));
        }

        /// <summary>
        /// Creates a Windows user.
        /// </summary>
        /// <param name="userName">Name of the user.</param>
        /// <param name="password">The password.</param>
        /// <param name="description">The description for the user.</param>
        public static void CreateUser(string userName, string password, string description)
        {
            using (var context = new PrincipalContext(ContextType.Machine))
            {
                using (UserPrincipal newUser = new UserPrincipal(context, userName, password, true))
                {
                    newUser.HomeDirectory = string.Format(CultureInfo.InvariantCulture, @"c:\users\{0}", userName);

                    newUser.Save();

                    DirectoryEntry de = newUser.GetUnderlyingObject() as DirectoryEntry;

                    if (!string.IsNullOrEmpty(description))
                    {
                        de.Properties["Description"].Add(description);
                    }

                    de.Invoke("Put", new object[] { "UserFlags", 0x10000 });   // 0x10000 is DONT_EXPIRE_PASSWORD 
                    de.Invoke("SetPassword", password);

                    newUser.Save();
                }
            }
        }

        /// <summary>
        /// Deletes the user.
        /// </summary>
        /// <param name="userName">Name of the user.</param>
        public static void DeleteUser(string userName)
        {
            using (DirectoryEntry localEntry = new DirectoryEntry("WinNT://.,Computer"))
            {
                DirectoryEntries localChildren = localEntry.Children;
                using (DirectoryEntry userEntry = localChildren.Find(userName, "User"))
                {
                    localChildren.Remove(userEntry);
                }
            }
        }

        /// <summary>
        /// Verify if the user exists.
        /// </summary>
        /// <param name="userName">The user name.</param>
        /// <returns>True if the user exists.</returns>
        public static bool UserExists(string userName)
        {
            return WindowsUsersAndGroups.Users.Contains(userName);
        }

        /// <summary>
        /// Gets all the existing windows groups.
        /// </summary>
        /// <returns>Local group names.</returns>
        public static string[] GetGroups()
        {
            List<string> users = new List<string>();
            
            using (var context = new PrincipalContext(ContextType.Machine))
            {
                using (var groupPrincipal = new GroupPrincipal(context))
                {
                    using (var searcher = new PrincipalSearcher())
                    {
                        foreach (var result in searcher.FindAll())
                        {
                            DirectoryEntry de = result.GetUnderlyingObject() as DirectoryEntry;
                            Console.WriteLine(de.Name);
                            users.Add(de.Name);
                        }
                    }
                }
            }
            
            return users.ToArray();
        }

        /// <summary>
        /// Creates a Windows group.
        /// </summary>
        /// <param name="groupName">Name of the group.</param>
        /// <param name="description">The description for the group.</param>
        public static void CreateGroup(string groupName, string description)
        {
            using (DirectoryEntry localEntry = new DirectoryEntry("WinNT://.,Computer"))
            {
                using (DirectoryEntry newGroup = localEntry.Children.Add(groupName, "Group"))
                {
                    if (!string.IsNullOrEmpty(description))
                    {
                        newGroup.Properties["Description"].Add(description);
                    }

                    newGroup.CommitChanges();
                }
            }
        }

        /// <summary>
        /// Deletes the group.
        /// </summary>
        /// <param name="groupName">Name of the group.</param>
        public static void DeleteGroup(string groupName)
        {
            using (DirectoryEntry localEntry = new DirectoryEntry("WinNT://.,Computer"))
            {
                DirectoryEntries localChildren = localEntry.Children;
                using (DirectoryEntry groupEntry = localChildren.Find(groupName, "Group"))
                {
                    localChildren.Remove(groupEntry);
                }
            }
        }

        /// <summary>
        /// Verify if the group exists.
        /// </summary>
        /// <param name="groupName">The group name.</param>
        /// <returns>True if group exists.</returns>
        public static bool ExistsGroup(string groupName)
        {
            string groupPath = string.Format(CultureInfo.InvariantCulture, "WinNT://./{0},Group", groupName);
            try
            {
                return DirectoryEntry.Exists(groupPath);
            }
            catch (COMException ex)
            {
                if (ex.Message.Contains("The specified local group does not exist."))
                {
                    return false;
                }
                else
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Adds a user to a group.
        /// </summary>
        /// <param name="userName">Name of the user.</param>
        /// <param name="groupName">Name of the group.</param>
        public static void AddUserToGroup(string userName, string groupName)
        {
            using (DirectoryEntry groupEntry = new DirectoryEntry("WinNT://./" + groupName + ",Group"))
            {
                groupEntry.Invoke("Add", new object[] { "WinNT://" + userName + ",User" });
                groupEntry.CommitChanges();
            }
        }

        /// <summary>
        /// Removes a user from a group.
        /// </summary>
        /// <param name="userName">Name of the user.</param>
        /// <param name="groupName">Name of the group.</param>
        public static void RemoveUserFromGroup(string userName, string groupName)
        {
            using (DirectoryEntry groupEntry = new DirectoryEntry("WinNT://./" + groupName + ",Group"))
            {
                groupEntry.Invoke("Remove", new object[] { "WinNT://" + userName + ",User" });
                groupEntry.CommitChanges();
            }
        }

        /// <summary>
        /// Determines whether [is user member of group] [the specified user name].
        /// </summary>
        /// <param name="userName">Name of the user.</param>
        /// <param name="groupName">Name of the group.</param>
        /// <returns>
        ///   <c>true</c> if [is user member of group] [the specified user name]; otherwise, <c>false</c>.
        /// </returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Returns false for any exception.")]
        public static bool IsUserMemberOfGroup(string userName, string groupName)
        {
            using (DirectoryEntry groupEntry = new DirectoryEntry("WinNT://./" + groupName + ",Group"))
            {
                try
                {
                    var userPath = string.Format(CultureInfo.InvariantCulture, "WinNT://{0}/{1}", Environment.MachineName, userName);
                    return (bool)groupEntry.Invoke("IsMember", new object[] { userPath });
                }
                catch
                {
                    return false;
                }
            }
        }

        public static string GetLocalUserSid(string userName)
        {
            return new NTAccount(null, userName).Translate(typeof(SecurityIdentifier)).Value;
        }
    }
}
