using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Principal;
using System.Transactions;
using static MiniBankSystemProject.Program;

namespace MiniBankSystemProject
{
    internal class Program
    {
        // =============================
        //        DATA STRUCTURES
        // =============================

        /// <summary>
        /// User object for login, signup, and linking accounts to logins.
        /// Each User has a username (login name), password, and role ("Admin" or "Customer").
        /// </summary>
        public class User
        {
            public string Username;   // The unique login name for this user
            public string Password;   // The login password for this user
            public string Role;       // "Admin" or "Customer"
        }

        // ----- Global Data Collections -----

        // Stores all registered users (login info)
        public static List<User> Users = new List<User>();
        // Stores all pending account creation requests (as queue, so admin approves in order)
        public static Queue<string> accountOpeningRequests = new Queue<string>();
        // Stores all approved account numbers (unique int per account)
        public static List<int> accountNumbersL = new List<int>();
        // Stores the login username that owns each account (parallel to accountNumbersL)
        public static List<string> accountNamesL = new List<string>();
        // Stores the balance of each account (parallel to accountNumbersL)
        public static List<double> balancesL = new List<double>();
        // Stores the National ID linked to each account (parallel to accountNumbersL)
        public static List<string> nationalIDsL = new List<string>();
        // Stores complaints/reviews as a stack (user can "undo" the last one)
        public static Stack<string> ReviewsS = new Stack<string>();
        // Last issued account number (for generating new unique numbers)
        static int lastAccountNumber = 1000;

        // ---- File storage ----
        const double MinimumBalance = 50.0;          // Minimum allowed balance in any account
        static string AccountsFilePath = "accounts.txt";   // File for saving/loading accounts
        static string UsersFilePath = "users.txt";         // File for saving/loading users
        static string ReviewsFilePath = "reviews.txt";     // File for saving/loading reviews
        static string TransactionsDir = "transactions";    // Folder for transaction logs/receipts


        // =============================
        //        DECORATIONS
        // =============================

        /// <summary>
        /// Prints the fancy ASCII logo/banner for the bank system at the top of every menu.
        /// </summary>
        static void PrintBankLogo()
        {
            // Custom bank logo/banner 
            Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                                 /\\                                       ║");
            Console.WriteLine("║                                /  \\                                      ║");
            Console.WriteLine("║                               / 🏦 \\                                     ║");
            Console.WriteLine("║                          /--------------\\                                ║");
            Console.WriteLine("║                         /  KHALFANOVISKI \\                               ║");
            Console.WriteLine("║                        /        BANK      \\                              ║");
            Console.WriteLine("║                       /____________________\\                             ║");
            Console.WriteLine("║   ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~    ║");
            Console.WriteLine("║   |    ___      ___      ___      ___      ___      ___     ___     |    ║");
            Console.WriteLine("║   |   |   |    |   |    |   |    |   |    |   |    |   |   |   |    |    ║");
            Console.WriteLine("║   |   |___|    |___|    |___|    |___|    |___|    |___|   |___|    |    ║");
            Console.WriteLine("║   |    ___      ___      ___      ___      ___      ___     ___     |    ║");
            Console.WriteLine("║   |   |   |    |   |    |   |    |   |    |   |    |   |   |   |    |    ║");
            Console.WriteLine("║   |   |___|    |___|    |___|    |___|    |___|    |___|   |___|    |    ║");
            Console.WriteLine("║   |                                                                 |    ║");
            Console.WriteLine("║   ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~    ║");
            Console.WriteLine("║                                                                          ║");
            Console.WriteLine("║            ||    Trusted | Luxury | Secure | Community    ||             ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════╝");
            Console.WriteLine("");
        }

        /// <summary>
        /// Prints a header box for any dialog or sub-menu .
        /// </summary>
        static void PrintBoxHeader(string title, string icon = "💰")
        {
            Console.WriteLine("╔════════════════════════════════════════════════════════╗");
            Console.WriteLine("║            " + icon + "  " + title.PadRight(40) + "║");
            Console.WriteLine("╠════════════════════════════════════════════════════════╣");
        }
        /// <summary>
        /// Prints the footer/closing line for dialog/sub-menu boxes.
        /// </summary>
        static void PrintBoxFooter()
        {
            Console.WriteLine("╚════════════════════════════════════════════════════════╝");
        }
        /// <summary>
        /// Pauses the program and prompts the user to press enter to continue.
        /// </summary>
        static void PauseBox()
        {
            Console.WriteLine("╔════════════════════════════════════════════════════════╗");
            Console.WriteLine("║ Press Enter to continue...                            ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════╝");
            Console.ReadLine();
        }

        // =============================
        //      FILE MANAGEMENT
        // =============================

        /// <summary>
        /// Saves all account data (account number, username, balance, national ID) to disk.
        /// </summary>
        static void SaveAccountsInformationToFile()
        {
            StreamWriter writer = new StreamWriter(AccountsFilePath);
            for (int i = 0; i < accountNumbersL.Count; i++)
                writer.WriteLine(accountNumbersL[i] + "," + accountNamesL[i] + "," + balancesL[i] + "," + nationalIDsL[i]);
            writer.Close();
        }

        /// <summary>
        /// Loads all saved account data from disk into the program.
        /// </summary>
        static void LoadAccountsInformationFromFile()
        {
            accountNumbersL.Clear(); accountNamesL.Clear(); balancesL.Clear(); nationalIDsL.Clear();
            if (!File.Exists(AccountsFilePath)) return;
            string[] lines = File.ReadAllLines(AccountsFilePath);
            for (int i = 0; i < lines.Length; i++)
            {
                string[] p = lines[i].Split(',');
                if (p.Length >= 4)
                {
                    accountNumbersL.Add(Convert.ToInt32(p[0]));
                    accountNamesL.Add(p[1]);
                    balancesL.Add(Convert.ToDouble(p[2]));
                    nationalIDsL.Add(p[3]);
                    if (Convert.ToInt32(p[0]) > lastAccountNumber)
                        lastAccountNumber = Convert.ToInt32(p[0]);
                }
            }
        }

        /// <summary>
        /// Saves all registered user (login) data to disk.
        /// </summary>
        static void SaveUsers()
        {
            StreamWriter writer = new StreamWriter(UsersFilePath);
            for (int i = 0; i < Users.Count; i++)
                writer.WriteLine(Users[i].Username + "," + Users[i].Password + "," + Users[i].Role);
            writer.Close();
        }
        /// <summary>
        /// Loads all registered user (login) data from disk.
        /// </summary>
        static void LoadUsers()
        {
            Users.Clear();
            if (!File.Exists(UsersFilePath)) return;
            string[] lines = File.ReadAllLines(UsersFilePath);
            for (int i = 0; i < lines.Length; i++)
            {
                string[] parts = lines[i].Split(',');
                if (parts.Length == 3)
                {
                    User user = new User();
                    user.Username = parts[0];
                    user.Password = parts[1];
                    user.Role = parts[2];
                    Users.Add(user);
                }
            }
        }

        /// <summary>
        /// Saves all complaints/reviews (stack) to disk.
        /// </summary>
        static void SaveReviews()
        {
            StreamWriter writer = new StreamWriter(ReviewsFilePath);
            foreach (string s in ReviewsS)
                writer.WriteLine(s);
            writer.Close();
        }
        /// <summary>
        /// Loads all complaints/reviews (stack) from disk.
        /// </summary>
        static void LoadReviews()
        {
            ReviewsS.Clear();
            if (!File.Exists(ReviewsFilePath)) return;
            string[] lines = File.ReadAllLines(ReviewsFilePath);
            for (int i = lines.Length - 1; i >= 0; i--)
                ReviewsS.Push(lines[i]);
        }

        /// <summary>
        /// Appends a transaction to a user's transaction log (txt file).
        /// </summary>
        static void LogTransaction(int accountIdx, string type, double amount, double balance)
        {
            if (!Directory.Exists(TransactionsDir)) Directory.CreateDirectory(TransactionsDir);
            string fn = TransactionsDir + "/acc_" + accountNumbersL[accountIdx] + ".txt";
            StreamWriter sw = new StreamWriter(fn, true);
            sw.WriteLine(DateTime.Now + " | " + type + " | Amount: " + amount + " | Balance: " + balance);
            sw.Close();
        }
        /// <summary>
        /// Shows all transactions for a given account.
        /// </summary>
        static void ShowTransactionHistory(int accountIdx)
        {
            string fn = TransactionsDir + "/acc_" + accountNumbersL[accountIdx] + ".txt";
            PrintBoxHeader("TRANSACTION HISTORY", "💸");
            if (!File.Exists(fn)) Console.WriteLine("|   No transactions found.                            |");
            else
            {
                string[] lines = File.ReadAllLines(fn);
                for (int i = 0; i < lines.Length; i++)
                    Console.WriteLine("|   " + lines[i].PadRight(48) + "|");
            }
            PrintBoxFooter();
        }

        /// <summary>
        /// After deposit/withdraw, prints a transaction receipt to a txt file with timestamp.
        /// </summary>
        public static void PrintReceipt(string type, int accIdx, double amt, double bal)
        {
            string fn = "receipt_" + accountNumbersL[accIdx] + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt";
            using (StreamWriter sw = new StreamWriter(fn))
            {
                sw.WriteLine("==== MiniBank Receipt ====");
                sw.WriteLine("Account Number: " + accountNumbersL[accIdx]);
                sw.WriteLine("Username: " + accountNamesL[accIdx]);
                sw.WriteLine("Operation: " + type);
                sw.WriteLine("Amount: " + amt.ToString("F2"));
                sw.WriteLine("Balance: " + bal.ToString("F2"));
                sw.WriteLine("Date: " + DateTime.Now);
            }
            Console.WriteLine($"Receipt saved as {fn}");
        }

        // =============================
        //         UTILITY LOGIC
        // =============================

        /// <summary>
        /// Returns true if the National ID is already used in an approved account or pending request.
        /// </summary>
        public static bool NationalIDExistsInRequestsOrAccounts(string nationalID)
        {
            if (nationalIDsL.Contains(nationalID)) return true;
            foreach (string req in accountOpeningRequests)
                if (req.Contains("National ID: " + nationalID)) return true;
            return false;
        }
        /// <summary>
        /// Returns the index of the logged-in user's account (by username), or -1 if none.
        /// </summary>
        public static int GetAccountIndexForUser(User currentUser)
        {
            for (int i = 0; i < accountNamesL.Count; i++)
                if (accountNamesL[i].ToLower() == currentUser.Username.ToLower())
                    return i;
            return -1;
        }
        /// <summary>
        /// Returns the pending request for this user, or null if none.
        /// </summary>
        public static string GetPendingRequestForUser(User currentUser)
        {
            foreach (string req in accountOpeningRequests)
                if (req.Contains("Username: " + currentUser.Username))
                    return req;
            return null;
        }

        // =============================
        //       MAIN NAVIGATION
        // =============================

        /// <summary>
        /// Program entry point. Loads all persistent data and launches the main menu.
        /// </summary>
        public static void Main(string[] args)
        {
            // Ensure transaction directory exists and launch the system
            if (!Directory.Exists(TransactionsDir)) Directory.CreateDirectory(TransactionsDir);
            StartSystem();
        }

        /// <summary>
        /// Loads persistent data and starts the main menu loop.
        /// </summary>
        public static void StartSystem()
        {
            LoadAccountsInformationFromFile();
            LoadReviews();
            LoadUsers();
            DisplayWelcomeMessage();
        }

        /// <summary>
        /// Shows the main "welcome" menu to choose between admin, customer, or exit.
        /// </summary>
        public static void DisplayWelcomeMessage()
        {
            while (true)
            {
                Console.Clear();
                PrintBankLogo();
                Console.WriteLine("╔════════════════════════════════════════════════════════╗");
                Console.WriteLine("║         [1] Admin Portal                              ║");
                Console.WriteLine("║         [2] Customer Portal                           ║");
                Console.WriteLine("║         [3] Login by National ID                      ║");
                Console.WriteLine("║         [0] Exit                                      ║");
                Console.WriteLine("╚════════════════════════════════════════════════════════╝");
                Console.Write("Enter your choice: ");
                string input = Console.ReadLine();
                if (input == "1") ShowRoleAuthMenu("Admin");
                else if (input == "2") ShowRoleAuthMenu("Customer");
                else if (input == "3")
                {
                    // Allows login by National ID only
                    User u = LoginByNationalID();
                    if (u != null) ShowCustomerMenu(u);
                }
                else if (input == "0") { ExitApplication(); break; }
                else { Console.WriteLine("Invalid choice! Try again."); PauseBox(); }
            }
        }

        /// <summary>
        /// Shows login/signup for Admin or Customer role.
        /// </summary>
        public static void ShowRoleAuthMenu(string role)
        {
            while (true)
            {
                Console.Clear();
                string banner = (role == "Admin") ? "🏦 ADMIN AUTHENTICATION 🏦   " : "👤 CUSTOMER AUTHENTICATION 👤";
                PrintBoxHeader(banner);
                Console.WriteLine("║ [1] Login                                              ║");
                Console.WriteLine("║ [2] Signup                                             ║");
                Console.WriteLine("║ [0] Back                                               ║");
                PrintBoxFooter();
                Console.Write("Enter your choice: ");
                string input = Console.ReadLine();
                if (input == "1")
                {
                    User user = LoginSpecificRole(role);
                    if (user != null)
                    {
                        if (role == "Admin") ShowAdminMenu(user);
                        else ShowCustomerMenu(user);
                    }
                }
                else if (input == "2") SignupSpecificRole(role);
                else if (input == "0") break;
                else { Console.WriteLine("Invalid choice! Try again."); PauseBox(); }
            }
        }
        /// <summary>
        /// Helper to return to the main welcome menu.
        /// </summary>
        public static void goBack() { Console.Clear(); DisplayWelcomeMessage(); }
        /// <summary>
        /// Saves all data and cleanly exits the program.
        /// </summary>
        public static void ExitApplication()
        {
            SaveAccountsInformationToFile();
            SaveUsers();
            SaveReviews();
            Console.Clear();
            PrintBoxHeader("Thank You For Banking With Us! 🏦");
            PrintBoxFooter();
            Environment.Exit(0);
        }

        // =============================
        //       LOGIN & SIGNUP
        // =============================

        /// <summary>
        /// Standard login by username and password for specified role.
        /// </summary>
        public static User LoginSpecificRole(string role)
        {
            Console.Clear();
            PrintBoxHeader("LOGIN " + role.ToUpper(), role == "Admin" ? "🛡️" : "👤");
            Console.Write("| Username: ");
            string username = Console.ReadLine();
            Console.Write("| Password: ");
            string password = Console.ReadLine();
            PrintBoxFooter();
            for (int i = 0; i < Users.Count; i++)
                if (Users[i].Username == username && Users[i].Password == password && Users[i].Role == role)
                {
                    Console.WriteLine("\nLogin successful!");
                    PauseBox();
                    return Users[i];
                }
            Console.WriteLine("\nInvalid credentials.");
            PauseBox();
            return null;
        }
        /// <summary>
        /// Signup for specified role, requiring unique username.
        /// </summary>
        public static void SignupSpecificRole(string role)
        {
            Console.Clear();
            PrintBoxHeader("SIGNUP " + role.ToUpper(), role == "Admin" ? "🛡️" : "👤");
            Console.Write("| Choose Username: ");
            string username = Console.ReadLine();
            for (int i = 0; i < Users.Count; i++)
                if (Users[i].Username == username)
                {
                    Console.WriteLine("| Username already exists!");
                    PrintBoxFooter(); PauseBox(); return;
                }
            Console.Write("| Choose Password: ");
            string password = Console.ReadLine();
            PrintBoxFooter();
            User u = new User();
            u.Username = username; u.Password = password; u.Role = role;
            Users.Add(u); SaveUsers();
            Console.WriteLine("\nSignup successful!");
            PauseBox();
        }

        /// <summary>
        /// Customer login using only National ID. Validates that account exists.
        /// </summary>
        public static User LoginByNationalID()
        {
            Console.Clear();
            PrintBoxHeader("LOGIN BY NATIONAL ID", "🔑");
            Console.Write("| Enter your National ID: ");
            string nationalID = Console.ReadLine();
            PrintBoxFooter();
            int idx = nationalIDsL.IndexOf(nationalID);
            if (idx == -1)
            {
                Console.WriteLine("No approved account with this National ID.");
                PauseBox();
                return null;
            }
            string username = accountNamesL[idx];
            User foundUser = null;
            for (int i = 0; i < Users.Count; i++)
                if (Users[i].Username == username && Users[i].Role == "Customer")
                    foundUser = Users[i];
            if (foundUser == null)
            {
                Console.WriteLine("No login linked to this National ID.");
                PauseBox();
                return null;
            }
            Console.WriteLine("Login successful! Welcome, " + username);
            PauseBox();
            return foundUser;
        }

        // =============================
        //          ADMIN MENU
        // =============================

        /// <summary>
        /// Main admin dashboard with all admin functions.
        /// </summary>
        public static void ShowAdminMenu(User currentUser)
        {
            while (true)
            {
                Console.Clear();
                PrintBankLogo();
                Console.WriteLine("  ╔════════════════════════════════════════════════════╗");
                Console.WriteLine("  ║         👑   ADMIN CONTROL CENTER   👑             ║");
                Console.WriteLine("  ╠════════════════════════════════════════════════════╣");
                Console.WriteLine("  ║  Welcome, " + currentUser.Username.PadRight(38) + "  ║");
                Console.WriteLine("  ╠════════════════════════════════════════════════════╣");
                Console.WriteLine("  ║ [1]  View Account Requests                         ║");
                Console.WriteLine("  ║ [2]  Process Account Requests                      ║");
                Console.WriteLine("  ║ [3]  View All Accounts                             ║");
                Console.WriteLine("  ║ [4]  Search Account                                ║");
                Console.WriteLine("  ║ [5]  Delete Account (by Account Number)            ║");
                Console.WriteLine("  ║ [6]  Export All Accounts                           ║");
                Console.WriteLine("  ║ [7]  Show Top Three Richest                        ║");
                Console.WriteLine("  ║ [8]  Show Total Bank Balance                       ║");
                Console.WriteLine("  ║ [9]  View Reviews                                  ║");
                Console.WriteLine("  ║ [10] View All Transaction                          ║");
                Console.WriteLine("  ║ [11] Search User Transaction                       ║");
                Console.WriteLine("  ║ [0]  Logout                                        ║");
                Console.WriteLine("  ╚════════════════════════════════════════════════════╝");
                Console.Write("  Choose: ");
                string ch = Console.ReadLine();
                switch (ch)
                {
                    case "1": ViewRequests(); break;
                    case "2": ProcessRequest(); break;
                    case "3": ViewAccounts(); break;
                    case "4": AdminSearchByNationalIDorName(); break;
                    case "5": AdminDeleteAccountByNumber(); break;
                    case "6": ExportAllAccountsToFile(); break;
                    case "7": ShowTopRichestCustomers(); break;
                    case "8": ShowTotalBankBalance(); break;
                    case "9": ViewReviews(); break;
                    case "10": ShowAllTransactionsForAllUsers(); break;
                    case "11": AdminSearchUserTransactions(); break;
                    case "0": return;
                    default: Console.WriteLine("Invalid choice!"); PauseBox(); break;
                }
            }
        }

        /// <summary>
        /// Shows all pending account opening requests for admin review.
        /// </summary>
        public static void ViewRequests()
        {
            Console.Clear();
            PrintBoxHeader("PENDING ACCOUNT REQUESTS", "📝");
            if (accountOpeningRequests.Count == 0)
                Console.WriteLine("|   No requests.                                     |");
            else
                foreach (string r in accountOpeningRequests)
                    Console.WriteLine("|   " + r.PadRight(48) + "|");
            PrintBoxFooter();
            PauseBox();
        }

        /// <summary>
        /// Allows admin to approve or reject each pending account opening request.
        /// </summary>
        public static void ProcessRequest()
        {
            Console.Clear();
            PrintBoxHeader("PROCESS REQUESTS", "🔎");
            if (accountOpeningRequests.Count == 0)
            {
                Console.WriteLine("|   No requests.                                     |");
                PrintBoxFooter();
                PauseBox(); return;
            }
            while (accountOpeningRequests.Count > 0)
            {
                string req = accountOpeningRequests.Peek();
                Console.WriteLine("|   " + req.PadRight(48) + "|");
                PrintBoxFooter();
                Console.Write("Approve (A) / Reject (R): ");
                char k = Console.ReadKey().KeyChar;
                Console.WriteLine();

                // Parse request fields for username, name, national ID, initial deposit
                string[] parts = req.Split('|');
                string username = "", name = "", nationalID = "";
                double initDeposit = 0.0;
                for (int i = 0; i < parts.Length; i++)
                {
                    if (parts[i].Trim().StartsWith("Username:")) username = parts[i].Replace("Username:", "").Trim();
                    if (parts[i].Trim().StartsWith("Name:")) name = parts[i].Replace("Name:", "").Trim();
                    if (parts[i].Trim().StartsWith("National ID:")) nationalID = parts[i].Replace("National ID:", "").Trim();
                    if (parts[i].Trim().StartsWith("Initial:")) double.TryParse(parts[i].Replace("Initial:", "").Trim(), out initDeposit);
                }

                if (k == 'A' || k == 'a')
                {
                    // Approve: create new account and remove request
                    int newAccountNumber = ++lastAccountNumber;
                    accountNumbersL.Add(newAccountNumber);
                    accountNamesL.Add(username);
                    balancesL.Add(initDeposit);
                    nationalIDsL.Add(nationalID);
                    SaveAccountsInformationToFile();
                    accountOpeningRequests.Dequeue();
                    Console.WriteLine("Account created. Number: " + newAccountNumber);
                }
                else if (k == 'R' || k == 'r')
                {
                    accountOpeningRequests.Dequeue();
                    Console.WriteLine("Request rejected.");
                }
                else { Console.WriteLine("Invalid input. Skipping..."); }
                if (accountOpeningRequests.Count == 0) break;
            }
            PauseBox();
        }

        /// <summary>
        /// Shows all approved bank accounts with details.
        /// </summary>
        public static void ViewAccounts()
        {
            Console.Clear();
            PrintBoxHeader("ALL ACCOUNTS", "📒");
            for (int i = 0; i < accountNumbersL.Count; i++)
            {
                string info = "Acc#: " + accountNumbersL[i] + " | User: " + accountNamesL[i] + " | Bal: " + balancesL[i] + " | NID: " + nationalIDsL[i];
                Console.WriteLine("|   " + info.PadRight(48) + "|");
            }
            PrintBoxFooter();
            PauseBox();
        }

        /// <summary>
        /// Allows admin to search by national ID or username. Shows account number and balance.
        /// </summary>
        public static void AdminSearchByNationalIDorName()
        {
            Console.Clear();
            PrintBoxHeader("SEARCH ACCOUNT", "🔍");
            Console.Write("| Enter National ID or Username: ");
            string query = Console.ReadLine();
            bool found = false;
            for (int i = 0; i < nationalIDsL.Count; i++)
            {
                if (nationalIDsL[i] == query || accountNamesL[i].Equals(query, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Account#: " + accountNumbersL[i] + " | Username: " + accountNamesL[i] +
                                      " | Balance: " + balancesL[i]);
                    found = true;
                }
            }
            if (!found) Console.WriteLine("No account found.");
            PauseBox();
        }

        /// <summary>
        /// Admin deletes an account by account number.
        /// </summary>
        public static void AdminDeleteAccountByNumber()
        {
            Console.Clear();
            PrintBoxHeader("DELETE ACCOUNT BY NUMBER", "🗑️");
            Console.Write("| Enter Account Number: ");
            string accNumStr = Console.ReadLine();
            int accNum;
            if (!int.TryParse(accNumStr, out accNum))
            {
                Console.WriteLine("Invalid account number!");
                PauseBox();
                return;
            }
            int idx = accountNumbersL.IndexOf(accNum);
            if (idx == -1)
            {
                Console.WriteLine("Account not found.");
                PauseBox();
                return;
            }
            // Remove all parallel list entries
            accountNumbersL.RemoveAt(idx);
            accountNamesL.RemoveAt(idx);
            balancesL.RemoveAt(idx);
            nationalIDsL.RemoveAt(idx);
            SaveAccountsInformationToFile();
            Console.WriteLine("Account deleted.");
            PauseBox();
        }

        /// <summary>
        /// Export all account data as CSV/txt.
        /// </summary>
        public static void ExportAllAccountsToFile()
        {
            StreamWriter sw = new StreamWriter("accounts_export.txt");
            sw.WriteLine("AccountNumber,Username,NationalID,Balance");
            for (int i = 0; i < accountNumbersL.Count; i++)
                sw.WriteLine(accountNumbersL[i] + "," + accountNamesL[i] + "," + nationalIDsL[i] + "," + balancesL[i]);
            sw.Close();
            Console.WriteLine("Exported to accounts_export.txt.");
            PauseBox();
        }

        /// <summary>
        /// Show the top 3 customers with the highest balances.
        /// </summary>
        public static void ShowTopRichestCustomers()
        {
            Console.Clear();
            PrintBoxHeader("TOP 3 RICHEST CUSTOMERS", "🏆");
            List<int> sorted = new List<int>();
            for (int i = 0; i < balancesL.Count; i++) sorted.Add(i);
            for (int i = 0; i < balancesL.Count; i++)
                for (int j = i + 1; j < balancesL.Count; j++)
                    if (balancesL[sorted[j]] > balancesL[sorted[i]])
                    { int tmp = sorted[i]; sorted[i] = sorted[j]; sorted[j] = tmp; }
            for (int k = 0; k < 3 && k < sorted.Count; k++)
            {
                int idx = sorted[k];
                Console.WriteLine("|   " + (k + 1) + ". User: " + accountNamesL[idx] + " | Acc#: " + accountNumbersL[idx] + " | Bal: " + balancesL[idx] + "   |");
            }
            PrintBoxFooter();
            PauseBox();
        }

        /// <summary>
        /// Show the total sum of all customer balances (bank's holdings).
        /// </summary>
        public static void ShowTotalBankBalance()
        {
            double total = 0.0;
            for (int i = 0; i < balancesL.Count; i++)
                total += balancesL[i];
            PrintBoxHeader("TOTAL BANK BALANCE", "💰");
            Console.WriteLine("|   Bank holds a total of: " + total.ToString("F2").PadRight(28) + "|");
            PrintBoxFooter();
            PauseBox();
        }

        /// <summary>
        /// Shows all transactions from all users/accounts in the bank.
        /// Each account's transaction history is read from its dedicated file.
        /// Used by Admin to audit all activity.
        /// </summary>
        public static void ShowAllTransactionsForAllUsers()
        {
            Console.Clear();
            PrintBoxHeader("ALL TRANSACTIONS (ALL USERS)", "💸");

            bool found = false;
            for (int i = 0; i < accountNumbersL.Count; i++)
            {
                string fn = TransactionsDir + "/acc_" + accountNumbersL[i] + ".txt";
                if (File.Exists(fn))
                {
                    string[] lines = File.ReadAllLines(fn);
                    if (lines.Length > 0)
                    {
                        Console.WriteLine("| Username: " + accountNamesL[i]);
                        foreach (string line in lines)
                        {
                            Console.WriteLine("|   " + line.PadRight(48) + "|");
                        }
                        found = true;
                    }
                }
            }
            if (!found)
            {
                Console.WriteLine("|   No transactions found for any user.               |");
            }
            PrintBoxFooter();
            PauseBox();
        }

        /// <summary>
        /// Allows Admin to search and display all transactions for a specific user by username.
        /// Reads the transaction history file for the selected account.
        /// </summary>        
        public static void AdminSearchUserTransactions()
        {
            Console.Clear();
            PrintBoxHeader("SEARCH USER TRANSACTIONS", "🔎");
            Console.Write("| Enter username: ");
            string searchUser = Console.ReadLine();
            bool found = false;

            for (int i = 0; i < accountNamesL.Count; i++)
            {
                if (accountNamesL[i].Equals(searchUser, StringComparison.OrdinalIgnoreCase))
                {
                    string fn = TransactionsDir + "/acc_" + accountNumbersL[i] + ".txt";
                    Console.WriteLine("| Transactions for: " + accountNamesL[i]);
                    if (File.Exists(fn))
                    {
                        string[] lines = File.ReadAllLines(fn);
                        foreach (string line in lines)
                        {
                            Console.WriteLine("|   " + line.PadRight(48) + "|");
                        }
                        found = true;
                    }
                    else
                    {
                        Console.WriteLine("|   No transactions found for this user.              |");
                        found = true;
                    }
                }
            }
            if (!found)
            {
                Console.WriteLine("|   No such username found in system.                 |");
            }
            PrintBoxFooter();
            PauseBox();
        }


        // =============================
        //        CUSTOMER MENU
        // =============================

        /// <summary>
        /// Main customer menu/dashboard. If account not approved, shows request status.
        /// </summary>
        public static void ShowCustomerMenu(User currentUser)
        {
            while (true)
            {
                int userIdx = GetAccountIndexForUser(currentUser);

                // User does not have an approved account yet
                if (userIdx == -1)
                {
                    string pendingReq = GetPendingRequestForUser(currentUser);
                    if (pendingReq != null)
                    {
                        PrintBoxHeader("ACCOUNT REQUEST STATUS", "📝");
                        Console.WriteLine("| Your account request is pending approval.           |");
                        PrintBoxFooter();
                        PauseBox();
                        return;
                    }
                    else
                    {
                        // Allow user to request an account if they have none
                        RequestAccountOpening(currentUser);
                        return;
                    }
                }

                // Approved account: show main customer menu
                Console.Clear();
                PrintBankLogo();
                Console.WriteLine("  ╔════════════════════════════════════════════════════╗");
                Console.WriteLine("  ║           💳   CUSTOMER DASHBOARD   💳            ║");
                Console.WriteLine("  ╠════════════════════════════════════════════════════╣");
                Console.WriteLine("  ║  Welcome, " + currentUser.Username.PadRight(38) + "║");
                Console.WriteLine("  ╠════════════════════════════════════════════════════╣");
                Console.WriteLine("  ║ [1]  Check Balance                                 ║");
                Console.WriteLine("  ║ [2]  Deposit                                       ║");
                Console.WriteLine("  ║ [3]  Withdraw                                      ║");
                Console.WriteLine("  ║ [4]  Transaction History                           ║");
                Console.WriteLine("  ║ [5]  Account Details                               ║");
                Console.WriteLine("  ║ [6]  Transfer Between Accounts                     ║");
                Console.WriteLine("  ║ [7]  Submit Review                                 ║");
                Console.WriteLine("  ║ [8]  Undo Last Complaint                           ║");
                Console.WriteLine("  ║ [0]  Logout                                        ║");
                Console.WriteLine("  ╚════════════════════════════════════════════════════╝");
                Console.Write("  Choose: ");
                string ch = Console.ReadLine();
                switch (ch)
                {
                    case "1": Console.WriteLine("Balance: " + balancesL[userIdx]); PauseBox(); break;
                    case "2": Deposit(userIdx); break;
                    case "3": Withdraw(userIdx); break;
                    case "4": ShowTransactionHistory(userIdx); PauseBox(); break;
                    case "5": AccountDetails(userIdx); break;
                    case "6": TransferBetweenAccounts(); break;
                    case "7": Reviews(); break;
                    case "8": UndoLastComplaint(); break;
                    case "0": return;
                    default: Console.WriteLine("Invalid choice!"); PauseBox(); break;
                }
            }
        }

        /// <summary>
        /// Lets customer request to open a new account (goes to pending requests).
        /// </summary>
        public static void RequestAccountOpening(User currentUser)
        {
            Console.Clear();
            PrintBoxHeader("REQUEST ACCOUNT OPENING", "📝");
            Console.Write("| Full Name: ");
            string name = Console.ReadLine();
            Console.Write("| National ID: ");
            string nationalID = Console.ReadLine();
            Console.Write("| Initial Deposit Amount: ");
            string initialDeposit = Console.ReadLine();
            PrintBoxFooter();
            if (NationalIDExistsInRequestsOrAccounts(nationalID))
            {
                Console.WriteLine("National ID already exists or pending.");
                PauseBox(); return;
            }
            string request = "Username: " + currentUser.Username + " | Name: " + name + " | National ID: " + nationalID + " | Initial: " + initialDeposit;
            accountOpeningRequests.Enqueue(request);
            Console.WriteLine("\nAccount request submitted!");
            PauseBox();
        }

        /// <summary>
        /// Deposit money for a customer account. Also prints a receipt.
        /// </summary>
        public static void Deposit(int idx)
        {
            Console.Write("Deposit amount: ");
            double amt;
            if (!double.TryParse(Console.ReadLine(), out amt) || amt <= 0) { Console.WriteLine("Invalid amount."); PauseBox(); return; }
            balancesL[idx] += amt;
            SaveAccountsInformationToFile();
            LogTransaction(idx, "Deposit", amt, balancesL[idx]);
            PrintReceipt("Deposit", idx, amt, balancesL[idx]);
            Console.WriteLine("Deposit successful. New Balance: " + balancesL[idx]);
            PauseBox();
        }

        /// <summary>
        /// Withdraw money for a customer account. Enforces minimum balance, prints receipt.
        /// </summary>
        public static void Withdraw(int idx)
        {
            Console.Write("Withdraw amount: ");
            double amt;
            if (!double.TryParse(Console.ReadLine(), out amt) || amt <= 0) { Console.WriteLine("Invalid amount."); PauseBox(); return; }
            if (balancesL[idx] - amt < MinimumBalance) { Console.WriteLine("Insufficient funds or below minimum balance."); PauseBox(); return; }
            balancesL[idx] -= amt;
            SaveAccountsInformationToFile();
            LogTransaction(idx, "Withdraw", amt, balancesL[idx]);
            PrintReceipt("Withdraw", idx, amt, balancesL[idx]);
            Console.WriteLine("Withdraw successful. New Balance: " + balancesL[idx]);
            PauseBox();
        }

        /// <summary>
        /// Allows transferring money between two account numbers.
        /// </summary>
        public static void TransferBetweenAccounts()
        {
            Console.Clear();
            PrintBoxHeader("TRANSFER BETWEEN ACCOUNTS", "💸");
            Console.Write("| From Account Number: ");
            int fromAcc = int.Parse(Console.ReadLine());
            int fromIdx = accountNumbersL.IndexOf(fromAcc);

            Console.Write("| To Account Number: ");
            int toAcc = int.Parse(Console.ReadLine());
            int toIdx = accountNumbersL.IndexOf(toAcc);

            if (fromIdx == -1 || toIdx == -1)
            {
                Console.WriteLine("Invalid account number(s)!");
                PauseBox();
                return;
            }

            Console.Write("| Amount to transfer: ");
            double amt = double.Parse(Console.ReadLine());

            if (amt <= 0 || balancesL[fromIdx] - amt < MinimumBalance)
            {
                Console.WriteLine("Insufficient funds or would drop below minimum balance!");
                PauseBox();
                return;
            }

            balancesL[fromIdx] -= amt;
            balancesL[toIdx] += amt;
            SaveAccountsInformationToFile();
            LogTransaction(fromIdx, "Transfer Out", amt, balancesL[fromIdx]);
            LogTransaction(toIdx, "Transfer In", amt, balancesL[toIdx]);
            Console.WriteLine("Transfer successful.");
            PauseBox();
        }

        /// <summary>
        /// Shows all account details (account number, username, national ID, balance).
        /// </summary>
        public static void AccountDetails(int idx)
        {
            PrintBoxHeader("ACCOUNT DETAILS", "🧾");
            Console.WriteLine("|   Account#: " + accountNumbersL[idx].ToString().PadRight(36) + "|");
            Console.WriteLine("|   Username: " + accountNamesL[idx].PadRight(41) + "|");
            Console.WriteLine("|   National ID: " + nationalIDsL[idx].PadRight(35) + "|");
            Console.WriteLine("|   Balance: " + balancesL[idx].ToString().PadRight(39) + "|");
            PrintBoxFooter();
            PauseBox();
        }

        /// <summary>
        /// Lets a customer submit a new review or complaint (pushes onto stack).
        /// </summary>
        public static void Reviews()
        {
            Console.Clear();
            PrintBoxHeader("SUBMIT COMPLAINT/REVIEW", "✉️");
            Console.Write("| Your complaint/review: ");
            string review = Console.ReadLine();
            ReviewsS.Push(review);
            SaveReviews();
            Console.WriteLine("Complaint submitted.");
            PrintBoxFooter();
            PauseBox();
        }

        /// <summary>
        /// Removes the most recent review/complaint submitted by any user.
        /// </summary>
        public static void UndoLastComplaint()
        {
            if (ReviewsS.Count > 0)
            {
                ReviewsS.Pop();
                SaveReviews();
                Console.WriteLine("Last complaint removed!");
            }
            else
                Console.WriteLine("No complaint to remove.");
            PauseBox();
        }

        /// <summary>
        /// Shows all submitted reviews/complaints (from the stack).
        /// </summary>
        public static void ViewReviews()
        {
            Console.Clear();
            PrintBoxHeader("ALL COMPLAINTS/REVIEWS", "✉️");
            if (ReviewsS.Count == 0) Console.WriteLine("|   No reviews.                                       |");
            else foreach (string s in ReviewsS) Console.WriteLine("|   " + s.PadRight(48) + "|");
            PrintBoxFooter();
            PauseBox();
        }


        


    }

}
