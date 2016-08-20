using ComponentFactory.Krypton.Navigator;
using ComponentFactory.Krypton.Toolkit;
using ComponentFactory.Krypton.Workspace;
using Microsoft.Win32;
using PoGo.NecroBot.Logic;
using PoGo.NecroBot.Logic.Common;
using PoGo.NecroBot.Logic.Event;
using PoGo.NecroBot.Logic.Logging;
using PoGo.NecroBot.Logic.PoGoUtils;
using PoGo.NecroBot.Logic.State;
using PoGo.NecroBot.Logic.Tasks;
using PoGo.NecroBot.Logic.Utils;
using PoGo.NecroBot.Logic.Model.Settings;
using POGOProtos.Map.Fort;
using POGOProtos.Networking.Responses;
using POGOProtos.Inventory.Item;
using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Generic;

namespace PoGo.NecroBot.GUI
{

    public partial class PokeGUI : KryptonForm
    {
        private DragManager _dm;
        private bool mapLoaded;
        private Session _session;
        private ListViewColumnSorter lvwColumnSorter;
        private ListViewColumnSorter lvwCatchesColumnSorter;
        private ListViewColumnSorter lvwTransfersColumnSorter;
        private ListViewColumnSorter lvwPokestopsColumnSorter;
        private ListViewColumnSorter lvwEvolvesColumnSorter;
        private Thread botThread;
        internal StateMachine machine;
        private int pokemonCaught;
        private int pokestopsVisited;
        private bool debugUI;
        private bool debugLogs;
        private bool debugMap;
        private bool snipeStarted;
        private string version;

        public PokeGUI()
        {
            this.mapLoaded = false;
            this.debugUI = false;
            this.debugLogs = false;
            this.snipeStarted = false;
            this.debugMap = false;
            this.pokemonCaught = 0;
            this.pokestopsVisited = 0;
            InitializeComponent();
            doComponentSettings();
            startUp();
        }

        private void doComponentSettings()
        {
            _dm = new DragManager();
            _dm.StateCommon.Feedback = PaletteDragFeedback.Block;
            _dm.DragTargetProviders.Add(workspaceDashboard);
            workspaceDashboard.DragPageNotify = _dm;
            workspaceDashboard.ShowMaximizeButton = true;
            lvwColumnSorter = new ListViewColumnSorter();
            lvwCatchesColumnSorter = new ListViewColumnSorter();
            lvwEvolvesColumnSorter = new ListViewColumnSorter();
            lvwTransfersColumnSorter = new ListViewColumnSorter();
            lvwPokestopsColumnSorter = new ListViewColumnSorter();
            this.listPokemon.ListViewItemSorter = lvwColumnSorter;
            this.listCatches.ListViewItemSorter = lvwCatchesColumnSorter;
            this.listEvolutions.ListViewItemSorter = lvwEvolvesColumnSorter;
            this.listTransfers.ListViewItemSorter = lvwTransfersColumnSorter;
            this.listPokestops.ListViewItemSorter = lvwPokestopsColumnSorter;
            lvwCatchesColumnSorter.SortColumn = 0;
            lvwEvolvesColumnSorter.SortColumn = 0;
            lvwTransfersColumnSorter.SortColumn = 0;
            lvwPokestopsColumnSorter.SortColumn = 0;
            lvwCatchesColumnSorter.Order = SortOrder.Descending;
            lvwEvolvesColumnSorter.Order = SortOrder.Descending;
            lvwTransfersColumnSorter.Order = SortOrder.Descending;
            lvwPokestopsColumnSorter.Order = SortOrder.Descending;
            this.listPokemonStats.Items.Clear();
            fixWebMap();

            this.version = Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
            this.Text = this.Text.Replace("{version}", this.version);
            Assembly a = typeof(ClientSettings).Assembly;
            this.Text = this.Text.Replace("{necrobotversion}", a.GetName().Version.ToString(3));
            columnExtra.Width = 0;
            systemId.Width = 0;
        }

        private void fixWebMap()
        {
            try
            {
                RegistryKey localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                var reg32 = localMachine.OpenSubKey(@"SOFTWARE\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION", true);
                var reg64 = localMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION", true);

                if (reg32.GetValue("PoGo.NecroBot.GUI.exe") == null)
                {
                    reg32.SetValue("PoGo.NecroBot.GUI.exe", 10000);
                }
                if (reg64.GetValue("PoGo.NecroBot.GUI.exe") == null)
                {
                    reg64.SetValue("PoGo.NecroBot.GUI.exe", 10000);
                }
            }
            catch (Exception ex)
            {
                Logger.Write("Unable to add registry key: " + ex.Message, LogLevel.Error);
            }
        }

        private void workspaceDashboard_WorkspaceCellAdding(object sender, WorkspaceCellEventArgs e)
        {
            e.Cell.NavigatorMode = NavigatorMode.BarRibbonTabGroup;
            e.Cell.Button.CloseButtonDisplay = ButtonDisplay.Hide;
            e.Cell.Button.ContextButtonDisplay = ButtonDisplay.Logic;
        }

        public void addPokemonCaught(string[] row)
        {
            this.pokemonCaught++;
            this.updateStatusCounters();

            this.UIThread(() =>
            {
                listCatches.Items.Add(new ListViewItem(row));
                this.listCatches.Sort();
            });

            this.getPokemons();
        }

        internal void sendEvent(string a, string b, string c, int d)
        {
            this.UIThread(() =>
            {
                if (this.webMap.Document != null && this.mapLoaded == true)
                {
                    Object[] objArray = new Object[4];
                    objArray[0] = (Object)a;
                    objArray[1] = (Object)b;
                    objArray[2] = (Object)c;
                    objArray[3] = (Object)d;
                    this.webMap.Document.InvokeScript("sendEvent", objArray);
                }
            });
        }
        public void addPokestopVisited(string[] row)
        {
            this.pokestopsVisited++;
            this.updateStatusCounters();
            this.UIThread(() =>
            {
                this.listPokestops.Items.Add(new ListViewItem(row));
                this.listPokestops.Sort();
            });
            this.updateInventory();
        }
        public void updateStatusCounters()
        {
            this.UIThread(delegate
            {
                this.labelPokestopsVisited.TextLine2 = this.pokestopsVisited.ToString();
                this.labelCaught.TextLine2 = this.pokemonCaught.ToString();
            });
        }

        public void SetControlText(string text, Control control, bool keep = false, bool addTime = false, string modify = "Text")
        {
            this.UIThread(() =>
            {
                if (addTime)
                {
                    text = $"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] {text}";
                }


                if (keep)
                {
                    if (control.Text == "..." || control.Text == ".")
                    {
                        control.Text = "";
                    }
                    control.Text = text + "\n" + control.Text;
                }
                else {
                    control.GetType().GetProperty(modify).SetValue(control, text, null);
                }
            });
        }

        private void SetList(ListView control, ListViewItem[] lvi, bool keep = false)
        {
            this.UIThread(() =>
            {
                if (!keep)
                {
                    control.Items.Clear();
                }
                foreach (var item in lvi)
                {
                    if (item != null)
                    {
                        control.Items.Add(item);
                    }
                }
            });
        }

        private void SetText(string text, Color color)
        {
            this.UIThread(() =>
            {
                this.textLog.AppendText(text, color);
                this.textLog.AppendText(Environment.NewLine);
                this.textLog.SelectionStart = this.textLog.Text.Length;
                this.textLog.ScrollToCaret();
            });
        }

        private void setLocation(double lng, double lat)
        {
            this.UIThread(() =>
            {
                if (this.webMap.Document != null && this.mapLoaded == true)
                {
                    Object[] objArray = new Object[2];
                    objArray[0] = (Object)lat;
                    objArray[1] = (Object)lng;
                    this.webMap.Document.InvokeScript("updateMarker", objArray);
                }
            });
        }

        private void setFort(string type, string id, string lng, string lat, string name, string extra)
        {
            this.UIThread(() =>
            {
                if (this.webMap.Document != null && this.mapLoaded == true)
                {
                    Object[] objArray = new Object[6];
                    objArray[0] = (Object)id;
                    objArray[1] = (Object)lng;
                    objArray[2] = (Object)lat;
                    objArray[3] = (Object)name;
                    objArray[4] = (Object)type;
                    objArray[5] = (Object)extra;
                    this.webMap.Document.InvokeScript("plotFort", objArray);
                }
            });
        }

        internal void setSniper(string lat, string lng)
        {
            this.UIThread(() =>
            {
                if (this.webMap.Document != null && this.mapLoaded == true)
                {
                    Object[] objArray = new Object[2];
                    objArray[0] = (Object)lat;
                    objArray[1] = (Object)lng;
                    this.webMap.Document.InvokeScript("addSnipeCircle", objArray);
                }
            });
        }

        private void setLogger()
        {
            Write writes = (string message, LogLevel level, Color color) =>
            {
                string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                if (level == LogLevel.Evolve)
                {
                    this.UIThread(() =>
                    {
                        listEvolutions.Items.Add(new ListViewItem(new string[] { now, message }));
                        listEvolutions.Sort();
                    });
                }
                else if (level == LogLevel.Transfer)
                {
                    this.UIThread(() =>
                    {
                        listTransfers.Items.Add(new ListViewItem(new string[] { now, message }));
                        listTransfers.Sort();
                    });
                }
                else if (level == LogLevel.Info && message.Contains("Playing"))
                {
                    this.onceLoaded();
                }
                else if (level == LogLevel.Info && message.Contains("Amount of Pokemon Caught"))
                {
                    Regex regex = new Regex(@"Caught:");
                    this.UIThread(() => { labelPokedex.TextLine2 = labelPokemonAmount.TextLine2 = regex.Split(message)[1]; });
                }
                else if (level == LogLevel.Update)
                {
                    string shorterMessage = message.Replace("! (", "@").Split('@')[0];
                    string secondMessage = "";
                    int maxLineLength = 35;
                    if (shorterMessage.Length > maxLineLength)
                    {
                        secondMessage = shorterMessage.Substring(maxLineLength);
                        shorterMessage = shorterMessage.Substring(0, maxLineLength);
                    }
                    this.UIThread(() =>
                    {
                        labelUpdate.TextLine1 = shorterMessage;
                        labelUpdate2.TextLine1 = secondMessage;
                    });

                }
                if (level != LogLevel.Debug || this.debugLogs == true)
                {
                    this.SetText($"[{now}] ({level.ToString()}) {message}", color);
                }
            };
            Logger.SetLogger(new EventLogger(LogLevel.Info, writes), "");
        }

        private void startUp()
        {
            setLogger();
            var settings = GlobalSettings.Load("");
            if (settings == null)
            {
                Logger.Write("This is your first start and the bot has generated the default config!", LogLevel.Warning);
                Logger.Write("We will now shutdown to let you configure the bot and then launch it again.", LogLevel.Warning);
                var x = MessageBox.Show("This is your first start and the bot has generated the default config!\nWe will now shutdown to let you configure the bot and then launch it again.", "Config created", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                if (x == DialogResult.OK)
                {
                    Environment.Exit(0);
                }
            }
            else {
                this.startBotThread(settings);
            }

        }

        private void startBotThread(GlobalSettings settings)
        {
            //botThread = new Thread(new ParameterizedThreadStart(botThreadWorker));
            //botThread.Start(settings);
            botThreadWorker(settings);
        }

        private async void botThreadWorker(object settingsObject)
        {
            var settings = (GlobalSettings)settingsObject;
            if (settings.ConsoleSettings == null)
            {
                settings.ConsoleSettings = new ConsoleConfig();
            }
            settings.ConsoleSettings.TranslationLanguageCode = "non-existing-translation-file-so-we-fallback-on-en";

            if (settings.UpdateSettings == null)
            {
                settings.UpdateSettings = new UpdateConfig();
            }
            settings.UpdateSettings.AutoUpdate = false;
            settings.UpdateSettings.CheckForUpdates = true;

            if (settings.GoogleWalkConfig == null)
            {
                settings.GoogleWalkConfig = new GoogleWalkConfig();
            }

            var session = new Session(new ClientSettings(settings), new LogicSettings(settings));
            _session = session;
            session.Client.ApiFailure = new ApiFailureStrategy(session);

            this.machine = new StateMachine();
            var stats = new Statistics();

            stats.DirtyEvent += () =>
                {
                    this.UIThread(delegate
                    {
                        this.labelAccount.TextLine2 = stats.GetTemplatedStats("{0}", "");
                        this.labelRuntime.TextLine2 = stats.GetTemplatedStats("{1}", "");
                        this.labelXpH.TextLine2 = stats.GetTemplatedStats("{3:0.0}", "");
                        this.labelPH.TextLine2 = stats.GetTemplatedStats("{4:0.0}", "");
                        this.labelStardust.TextLine2 = stats.GetTemplatedStats("{5:n0}", "");
                        this.labelTransferred.TextLine2 = stats.GetTemplatedStats("{6}", "");
                        this.labelRecycledCount.TextLine2 = stats.GetTemplatedStats("{7}", "");

                        this.labelLevel.TextLine2 = stats.GetTemplatedStats("{2}", "{0}");
                        this.labelNextLevel.TextLine2 = stats.GetTemplatedStats("{2}", "{1}h {2}m");
                        this.labelXp.TextLine2 = stats.GetTemplatedStats("{2}", "{3:n0}/{4:n0}");
                        this.labelSpeed.TextLine2 = this._session.LogicSettings.WalkingSpeedInKilometerPerHour + "km/h";
                    });
                };

            var aggregator = new StatisticsAggregator(stats);
            var listener = new EventListener(this);
            session.EventDispatcher.EventReceived += (IEvent evt) => listener.Listen(evt, session);
            session.EventDispatcher.EventReceived += (IEvent evt) => aggregator.Listen(evt, session);
            this.machine.SetFailureState(new LoginState());
            Logger.SetLoggerContext(session);


            session.Navigation.WalkStrategy.UpdatePositionEvent += (lat, lng) =>
            {
                session.EventDispatcher.Send(new UpdatePositionEvent { Latitude = lat, Longitude = lng });
                this.UIThread(delegate
                {
                    this.labelLat.TextLine2 = lat.ToString("0.00000");
                    this.labelLong.TextLine2 = lng.ToString("0.00000");
                });
                this.setLocation(lng, lat);
            };

            var profilePath = Path.Combine(Directory.GetCurrentDirectory());
            var workspaceFile = Path.Combine(profilePath, "workspace.xml");

            if (File.Exists(workspaceFile))
            {
                Logger.Write("Found a workspace.xml file, loading...");
                try
                {
                    workspaceDashboard.LoadLayoutFromFile(openFileDialog.FileName);
                }
                catch (Exception ex)
                {
                    Logger.Write("Unable to load workspace.xml. " + ex.Message, LogLevel.Error);
                }

            }
            else {
                Logger.Write("There is no workspace.xml in your root directory to load.");
            }

            if (this.debugUI)
            {
                return;
            }
            
            string now = DateTime.Now.ToString("yyyyMMddHHmm");
            string filename = $"http://rawgit.com/vandernorth/NecroBot.GUI/master/Map/getMap.html?date={now}&lat={settings.LocationSettings.DefaultLatitude}&long={settings.LocationSettings.DefaultLongitude}&radius={settings.LocationSettings.MaxTravelDistanceInMeters}&version={this.version}";
            if (debugMap == true)
            {
                filename = Application.StartupPath + $"\\Map\\getMap.html?lat={settings.LocationSettings.DefaultLatitude}&long={settings.LocationSettings.DefaultLongitude}&radius={settings.LocationSettings.MaxTravelDistanceInMeters}";
            }
            Logger.Write("Setting map location to " + filename, LogLevel.Debug);
            this.webMap.ScriptErrorsSuppressed = !debugMap;
            this.webMap.Url = new Uri(filename);

            if (settings.TelegramSettings.UseTelegramAPI)
            {
                session.Telegram = new Logic.Service.TelegramService(settings.TelegramSettings.TelegramAPIKey, session);
            }

            if (session.LogicSettings.UseSnipeLocationServer)
            {
                await SnipePokemonTask.AsyncStart(session);
                this.snipeStarted = true;
            }

            settings.checkProxy(session.Translation);
            await machine.AsyncStart(new VersionCheckState(), session);
        }
        private void onceLoaded()
        {
            this.runUpdate();
            this.setLocation(this._session.Settings.DefaultLongitude, this._session.Settings.DefaultLatitude);
            this.sendEvent("Lifecycle", "login", this._session.Profile.PlayerData.Username, 0);
        }

        private void webMap_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            this.mapLoaded = true;
            this.webMap.ScriptErrorsSuppressed = !debugMap;
        }

        private async void runUpdate()
        {
            await this._session.Inventory.RefreshCachedInventory();
            await this.getInventory();
            await this.getPokemons();
            this.getEggs();
            this.getForts();
        }

        private async void updateInventory()
        {
            try
            {
                await this._session.Inventory.RefreshCachedInventory();
                await this.getInventory();
            }
            catch (Exception ex)
            {
                Logger.Write($"updateInventory() failed. {ex.Message}", LogLevel.Error);
            }
        }
        private async void getForts()
        {
            try
            {

                var mapObjects = await this._session.Client.Map.GetMapObjects();

                // Wasn't sure how to make this pretty. Edit as needed.
                var forts = mapObjects.Item1.MapCells.SelectMany(i => i.Forts);
                foreach (var fort in forts)
                {
                    FortDetailsResponse tmp = new FortDetailsResponse();
                    tmp.Name = "Unknown name";
                    tmp.Description = "";

                    var fortInfo = tmp;// await this._session.Client.Fort.GetFort(fort.Id, fort.Latitude, fort.Longitude);
                    if (fort.Type == FortType.Checkpoint)
                    {
                        bool hasLure = fort.LureInfo != null && fort.LureInfo.LureExpiresTimestampMs > 0;
                        long exp = hasLure ? fort.LureInfo.LureExpiresTimestampMs : 0;
                        string marker = hasLure ? "fort-lure.png" : "fort-pokestop.png";
                        string extra = $"{{\"hasLure\": {hasLure.ToString().ToLower()}, \"lureGone\": {exp}, \"Description\": \"{fortInfo.Description}\", \"ImageUrls\": \"{/*fortInfo.ImageUrls.ToString().Replace("\"","'")*/""}\",\"marker\" : \"{marker}\" }}";
                        this.setFort(fort.Type.ToString(), fort.Id, fort.Longitude.ToString(), fort.Latitude.ToString(), fortInfo.Name, extra);
                    }
                    else if (fort.Type == FortType.Gym)
                    {
                        bool hasLure = fort.LureInfo != null && fort.LureInfo.LureExpiresTimestampMs > 0;
                        long exp = hasLure ? fort.LureInfo.LureExpiresTimestampMs : 0;
                        string marker = $"fort-{fort.OwnedByTeam.ToString().ToLower()}.png";
                        string extra = $"{{\"GuardPokemonCp\": {fort.GuardPokemonCp}, \"GuardPokemonId\": \"{fort.GuardPokemonId.ToString()}\", \"Description\": \"{fortInfo.Description}\", \"ImageUrls\": \"{fortInfo.ImageUrls.ToString().Replace("\"", "")}\",\"marker\": \"{marker}\" }}";
                        this.setFort(fort.Type.ToString(), fort.Id, fort.Longitude.ToString(), fort.Latitude.ToString(), fortInfo.Name, extra);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Write($"getForts() failed. {ex.Message}", LogLevel.Error);
            }
        }

        public void updateIncubator(string id, string kmRemaining)
        {
            this.UIThread(() =>
            {
                if (listEggs.Items.Count > 0)
                {
                    var item = listEggs.Items.Find(id, false).First();
                    if (item != null)
                    {
                        item.Text = kmRemaining + "km /" + item.Text.Split('/')[1];
                    }
                    else {
                        Logger.Write("Unable to find egg ass. with incubator " + id);
                    }
                }
            });
        }
        private async void getEggs()
        {
            var eggs = await this._session.Inventory.GetEggs();
            int eggsIncubating = 0;

            this.UIThread(() =>
            {
                listEggs.Items.Clear();
                foreach (var egg in eggs)
                {
                    if (string.IsNullOrEmpty(egg.EggIncubatorId))
                    {
                        ListViewItem lviEgg = listEggs.Items.Add($"{egg.EggKmWalkedTarget.ToString("F0")}km");
                        lviEgg.ImageKey = "egg";
                    }
                    else {
                        ListViewItem lviEgg = listEggs.Items.Add($"{egg.EggKmWalkedStart.ToString("F3")}km / {egg.EggKmWalkedTarget.ToString("F0")}km");
                        lviEgg.ImageKey = "egg";
                        lviEgg.Name = egg.EggIncubatorId;
                        eggsIncubating++;
                    }

                }
                labelEggCount.TextLine2 = eggsIncubating.ToString();
            });
        }

        private struct pokemonItem {
            public ListViewItem lvi;
            public bool addedToList;
        }
        private Dictionary<ulong, pokemonItem> allPokemonItems;
        private async Task getPokemons()
        {
            try
            {
                if (allPokemonItems == null) {
                    allPokemonItems = new Dictionary<ulong, pokemonItem>();
                }
                var items = await this._session.Inventory.GetPokemons();
                var myPokemonSettings = await this._session.Inventory.GetPokemonSettings();
                var pokemonSettings = myPokemonSettings.ToList();
                var myPokemonFamilies = await this._session.Inventory.GetPokemonFamilies();
                var pokemonFamilies = myPokemonFamilies.ToArray();
                var pokemonPairedWithStatsCp = items.Select(pokemon => Tuple.Create(pokemon, PokemonInfo.CalculateMaxCp(pokemon), PokemonInfo.CalculatePokemonPerfection(pokemon), PokemonInfo.GetLevel(pokemon))).ToList();

                bool[] pokemonsIHave = new bool[151];
                ListViewItem[] lvis = new ListViewItem[items.Count()];
                int index = 0;
                foreach (var item in pokemonPairedWithStatsCp)
                {
                    string thisName = !String.IsNullOrEmpty(item.Item1.Nickname) ? item.Item1.Nickname : item.Item1.PokemonId.ToString();
                    string cpInfo = $"{item.Item1.Cp}";
                    string cpPrcnt = (((double)item.Item1.Cp / (double)item.Item2) * 100).ToString("F0") + "%";
                    string canEvolve = "";
                    string thisCandies = "";

                    var settings = pokemonSettings.Single(x => x.PokemonId == item.Item1.PokemonId);
                    var familyCandy = pokemonFamilies.Single(x => settings.FamilyId == x.FamilyId);
                    if (settings.EvolutionIds.Count == 0)
                    {
                        canEvolve = "";
                        thisCandies = "";
                        thisCandies = $"{familyCandy.Candy_}";
                    }
                    else if (familyCandy.Candy_ - settings.CandyToEvolve > 0)
                    {
                        canEvolve = "yes";
                        int canEvolveThisMuch = familyCandy.Candy_ / settings.CandyToEvolve;
                        thisCandies = $"{canEvolveThisMuch}x ({familyCandy.Candy_} / {settings.CandyToEvolve})";
                    }
                    else {
                        canEvolve = $"Need {settings.CandyToEvolve - familyCandy.Candy_ } more candies";
                        thisCandies = $"{familyCandy.Candy_} / {settings.CandyToEvolve}";
                    }

                    string extra = $"";
                    string isFavorite = item.Item1.Favorite == 1 ? "Yes" : "";
                    var timeSpan = DateTime.FromFileTimeUtc(this.FromUnixTime(item.Item1.CreationTimeMs / 1000).ToFileTime());
                    var localDateTime = timeSpan.ToString("yyyy-MM-dd HH:mm:ss");
                    string[] row = {
                        thisName,
                        ((int)(item.Item1.PokemonId)).ToString(),
                        cpInfo,
                        item.Item2.ToString("F0"),
                        cpPrcnt,
                        item.Item3.ToString("F0") + "%",
                        item.Item4.ToString(),
                        canEvolve,
                        thisCandies,
                        extra,
                        item.Item1.Id.ToString(),
                        item.Item1.Move1.ToString(),
                        item.Item1.Move2.ToString(),
                        localDateTime,
                        $"{item.Item1.HeightM:0.00}m",
                        $"{item.Item1.WeightKg:0.00}kg", $"{item.Item1.Stamina}/{item.Item1.StaminaMax}",
                        item.Item1.NumUpgrades.ToString(),
                        item.Item1.IndividualAttack.ToString(),
                        item.Item1.IndividualDefense.ToString(),
                        item.Item1.IndividualStamina.ToString(),
                        isFavorite, item.Item1.BattlesAttacked.ToString(),
                        item.Item1.BattlesDefended.ToString(),
                        item.Item1.DeployedFortId
                    };

                    this.UIThread(()=> {
                        ListViewItem thisOne = new ListViewItem(row);
                        pokemonItem pi;
                        if (allPokemonItems.TryGetValue(item.Item1.Id, out pi))
                        {
                            for (int i = 0; i < pi.lvi.SubItems.Count; i++) {
                                pi.lvi.SubItems[i] = thisOne.SubItems[i];
                            }
                        }
                        else
                        {
                            pi = new pokemonItem();
                            pi.addedToList = false;
                            pi.lvi = thisOne;
                            allPokemonItems.Add(item.Item1.Id, pi);
                            thisOne.ImageIndex = (int)(item.Item1.PokemonId);
                            pokemonsIHave[(int)(item.Item1.PokemonId)] = true;
                            lvis[index] = thisOne;
                            index++;
                        }
                    });
                  
                   
                }
                Logger.Write($"Adding {lvis.Count()} to list");
                this.UIThread(() =>
                {
                    labelPokemonCount.TextLine2 = lvis.Count().ToString();
                });
                this.SetList(listPokemon, lvis, true);

                pokemonsIHave[144] = true;
                pokemonsIHave[145] = true;
                pokemonsIHave[146] = true;
                this.UIThread(() =>
                {
                    this.listPokemonStats.Clear();
                    for (int i = 1; i < 149; i++)
                    {
                        if (pokemonsIHave[i] != true)
                        {
                            ListViewItem thisOne = new ListViewItem(((POGOProtos.Enums.PokemonId)i).ToString());
                            thisOne.ImageIndex = i;
                            listPokemonStats.Items.Add(thisOne);
                        }
                    }
                });


                var pokemonToEvolveTask = await this._session.Inventory.GetPokemonToEvolve(this._session.LogicSettings.PokemonsToEvolve);
                var pokemonToEvolve = pokemonToEvolveTask.ToList();
                Logger.Write($"Pokemons to evolve = {pokemonToEvolve.Count}");
            }
            catch (Exception ex)
            {
                Logger.Write($"getPokemon() failed. {ex.Message}", LogLevel.Error);
                Logger.Write($"getPokemon() failed. {ex.StackTrace}", LogLevel.Error);
            }
        }

        public DateTime FromUnixTime(ulong unixTime)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddSeconds(unixTime);
        }
        private async Task getInventory()
        {
            try
            {
                var items = await this._session.Inventory.GetItems();
                try
                {
                    ListViewItem[] lvis = new ListViewItem[items.Count()];
                    int index = 0;
                    int totalItems = 0;
                    foreach (var item in items)
                    {
                        POGOProtos.Inventory.Item.ItemType itemType = (POGOProtos.Inventory.Item.ItemType)item.ItemId;
                        string[] row = { item.ItemId.ToString().Replace("Item", "").Replace("TroyDisk", "Lure"), item.Count.ToString(), "" };
                        ListViewItem thisOne = new ListViewItem(row);
                        lvis[index] = thisOne;
                        index++;
                        totalItems += item.Count;
                    }
                    this.SetList(listInv, lvis);
                    this.UIThread(() => labelItemCount.TextLine2 = totalItems.ToString());
                }
                catch (Exception e)
                {
                    Logger.Write("Error" + e.Message, LogLevel.Error);
                }
            }
            catch (Exception ex)
            {
                Logger.Write($"getInventory() failed. {ex.Message}", LogLevel.Error);
            }
        }

        private async void useIncense()
        {
            try
            {
                var incense = await this._session.Client.Inventory.UseIncense(POGOProtos.Inventory.Item.ItemId.ItemIncenseOrdinary);
                Logger.Write($"Incense result: {incense.Result} item: {incense.AppliedIncense}", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Logger.Write($"useIncense() failed. {ex.Message}", LogLevel.Error);
            }
        }

        private void useIncenseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Logger.Write("Using incense...", LogLevel.Info);
            Task task = new Task(useIncense);
            task.Start();
        }

        private void refreshInventoryAndPokemonToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Task task = new Task(runUpdate);
            task.Start();
        }



        private void evolveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listPokemon.SelectedItems.Count == 1)
            {
                ListViewItem selected = listPokemon.SelectedItems[0];
                string name = selected.SubItems[0].Text;
                ulong id = ulong.Parse(selected.SubItems[10].Text);
                DialogResult result = MessageBox.Show($"Do you want to evolve {name}?", $"Evolving {name}", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == System.Windows.Forms.DialogResult.Yes)
                {
                    this.evolvePokemon(id);

                }
            }
            else {
                var approx = ((this._session.LogicSettings.DelayBetweenPlayerActions + 1) * listPokemon.SelectedItems.Count) / 1000;
                DialogResult result = MessageBox.Show($"Do you want to evolve {listPokemon.SelectedItems.Count} pokemon? \nThis wil take approx {approx} seconds. Please wait patiently.", $"Evolving multiple pokemon", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    ulong[] evolveList = new ulong[listPokemon.SelectedItems.Count];
                    for (int i = 0; i < listPokemon.SelectedItems.Count; i++)
                    {
                        evolveList[i] = ulong.Parse(listPokemon.SelectedItems[i].SubItems[10].Text);
                    }
                    this.evolveMultiple(evolveList);
                }
            }
        }

        private void transferToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listPokemon.SelectedItems.Count == 1)
            {
                ListViewItem selected = listPokemon.SelectedItems[0];
                string name = selected.SubItems[0].Text;
                ulong id = ulong.Parse(selected.SubItems[10].Text);
                DialogResult result = MessageBox.Show($"Do you want to transfer {name}?", $"Transferring {name}", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    this.transfer(id);
                }
            }
            else {
                var approx = ((this._session.LogicSettings.DelayBetweenPlayerActions + 1) * listPokemon.SelectedItems.Count) / 1000;
                DialogResult result = MessageBox.Show($"Do you want to transfer {listPokemon.SelectedItems.Count} pokemon? \nThis wil take approx {approx} seconds. Please wait patiently.", $"Transferring multiple pokemon", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == System.Windows.Forms.DialogResult.Yes)
                {
                    ulong[] transferList = new ulong[listPokemon.SelectedItems.Count];
                    for (int i = 0; i < listPokemon.SelectedItems.Count; i++)
                    {
                        transferList[i] = ulong.Parse(listPokemon.SelectedItems[i].SubItems[10].Text);
                    }
                    this.transferMultiple(transferList);
                }
            }
        }

        private void powerUpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listPokemon.SelectedItems.Count == 1)
            {
                ListViewItem selected = listPokemon.SelectedItems[0];
                string name = selected.SubItems[0].Text;
                ulong id = ulong.Parse(selected.SubItems[10].Text);
                DialogResult result = MessageBox.Show($"Do you want to power up {name}?", $"Power up {name}", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    this.powerUp(id);
                }
            }
            else {
                MessageBox.Show($"Unable to powerup multiple pokemon at the same time", $"Sorry...", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        private async void transfer(ulong id, bool runUpdate = true)
        {
            try
            {
                ReleasePokemonResponse rps = await this._session.Client.Inventory.TransferPokemon(id);
                MessageBox.Show($"Transfer result: {rps.Result}\nCandies retrieved: {rps.CandyAwarded.ToString()}", "Transfer result", MessageBoxButtons.OK, MessageBoxIcon.Information);
                if (runUpdate) { await this.getPokemons(); }
            }
            catch (Exception ex)
            {
                Logger.Write($"transfer() failed. {ex.Message}", LogLevel.Error);
            }

        }

        private async void transferMultiple(ulong[] ids)
        {
            try
            {
                foreach (ulong id in ids)
                {
                    ReleasePokemonResponse rps = await this._session.Client.Inventory.TransferPokemon(id);
                    Logger.Write($"Transfer result: {rps.Result} - Candies retrieved: {rps.CandyAwarded.ToString()} id: {id.ToString()}", LogLevel.Transfer);
                    Logger.Write($"Transfer result: {rps.Result} - Candies retrieved: {rps.CandyAwarded.ToString()} id: {id.ToString()}");
                    DelayingUtils.Delay(this._session.LogicSettings.DelayBetweenPlayerActions, 0);
                }
                await this.getPokemons();
                Logger.Write($"Transfer of {ids.Length} pokemons completed");
            }
            catch (Exception ex)
            {
                Logger.Write($"transferMultiple() failed. {ex.Message}", LogLevel.Error);
            }
        }

        private async void evolveMultiple(ulong[] ids)
        {
            try
            {
                foreach (ulong id in ids)
                {
                    EvolvePokemonResponse eps = await this._session.Client.Inventory.EvolvePokemon(id);
                    Logger.Write($"Evolve result: {eps.EvolvedPokemonData.PokemonId} CP: {eps.EvolvedPokemonData.Cp} XP: {eps.ExperienceAwarded.ToString()} id: {id.ToString()}", LogLevel.Evolve);
                    Logger.Write($"Evolve result: {eps.EvolvedPokemonData.PokemonId} CP: {eps.EvolvedPokemonData.Cp} XP: {eps.ExperienceAwarded.ToString()} id: {id.ToString()}");
                    DelayingUtils.Delay(this._session.LogicSettings.DelayBetweenPlayerActions, 0);
                }
                await this.getPokemons();
                Logger.Write($"Evolving of {ids.Length} pokemons completed");
            }
            catch (Exception ex)
            {
                Logger.Write($"evolveMultiple() failed. {ex.Message}", LogLevel.Error);
            }
        }

        private async void evolvePokemon(ulong id, bool runUpdate = true)
        {
            try
            {
                EvolvePokemonResponse eps = await this._session.Client.Inventory.EvolvePokemon(id);
                if (eps.EvolvedPokemonData != null)
                {
                    MessageBox.Show($"Evolve result: {eps.EvolvedPokemonData.PokemonId} CP: {eps.EvolvedPokemonData.Cp}\nXP: {eps.ExperienceAwarded.ToString()}", "Evolve result", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    if (runUpdate) { await this.getPokemons(); }
                }
                else {
                    MessageBox.Show($"Unable to evolve: {eps.Result}", "Evolve failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                Logger.Write($"evolvePokemon() failed. {ex.Message}", LogLevel.Error);
            }
        }

        private async void powerUp(ulong id, bool runUpdate = true)
        {
            try
            {
                UpgradePokemonResponse ups = await this._session.Client.Inventory.UpgradePokemon(id);
                MessageBox.Show($"PowerUp result: {ups.Result}\nCP: {ups.UpgradedPokemon.Cp}", $"PowerUp result for {ups.UpgradedPokemon.PokemonId.ToString()}", MessageBoxButtons.OK, MessageBoxIcon.Information);
                if (runUpdate) { await this.getPokemons(); }
            }
            catch (Exception ex)
            {
                Logger.Write($"powerUp() failed. {ex.Message}", LogLevel.Error);
            }
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            Task task = new Task(runUpdate);
            task.Start();
            btnRefresh.Checked = ButtonCheckState.Checked;
        }

        private void snipeButton_Click(object sender, EventArgs e)
        {
            this.snipeNow();

        }

        private CancellationTokenSource snipeOnceCancellationSource = new CancellationTokenSource();
        private CancellationToken snipeOnceCancellationToken;
        private CancellationToken snipeCancellationToken;
        private async void snipeNow()
        {
            if (!this.snipeStarted)
            {
                await SnipePokemonTask.Start(this._session, snipeCancellationToken);
            }

            if (snipeOnceCancellationToken != null && !snipeOnceCancellationToken.IsCancellationRequested && snipeOnceCancellationToken.CanBeCanceled)
            {
                snipeOnceCancellationSource.Cancel();
            }

            snipeOnceCancellationSource = new CancellationTokenSource();
            snipeOnceCancellationToken = snipeOnceCancellationSource.Token;
            await SnipePokemonTask.Execute(this._session, snipeOnceCancellationToken);
        }

        private void renameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listPokemon.SelectedItems.Count == 1)
            {
                ListViewItem selected = listPokemon.SelectedItems[0];
                string name = selected.SubItems[0].Text;
                ulong id = ulong.Parse(selected.SubItems[10].Text);
                string newName = Prompt.ShowDialog(String.Format("What do you want the new name for {0} want to be?", name), "Rename pokemon");
                if (!String.IsNullOrEmpty(newName))
                {
                    this.rename(id, newName);
                }
                else {
                    Logger.Write("New name was empty, not setting it!");
                }
            }
            else {
                MessageBox.Show($"Unable to rename multiple pokemon at the same time", $"Sorry...", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }


        }

        private async void rename(ulong id, string newName)
        {
            try
            {
                await this._session.Client.Inventory.NicknamePokemon(id, newName);
                await this.getPokemons();
            }
            catch (Exception ex)
            {
                Logger.Write($"rename() failed. {ex.Message}", LogLevel.Error);
            }
        }

        private void saveWorkspace_Click(object sender, EventArgs e)
        {
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
                workspaceDashboard.SaveLayoutToFile(saveFileDialog.FileName);
        }

        private void loadWorkspace_Click(object sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    workspaceDashboard.LoadLayoutFromFile(openFileDialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error Loading from File");
                }
            }
        }

        private void btnClearLogs_Click(object sender, EventArgs e)
        {
            textLog.Text = "";
            btnClearLogs.Checked = ButtonCheckState.Checked;
        }

        private void sort(ListView lv, ColumnClickEventArgs e)
        {
            ListViewColumnSorter lvs = (ListViewColumnSorter)lv.ListViewItemSorter;
            if (e.Column == lvs.SortColumn)
            {
                if (lvs.Order == SortOrder.Ascending)
                {
                    lvs.Order = SortOrder.Descending;
                }
                else
                {
                    lvs.Order = SortOrder.Ascending;
                }
            }
            else
            {
                lvs.SortColumn = e.Column;
                lvs.Order = SortOrder.Ascending;
            }

            // Perform the sort with these new sort options.
            lv.Sort();
        }
        private void listCatches_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            this.sort((ListView)sender, e);
        }
        private void listPokestops_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            this.sort((ListView)sender, e);
        }
        private void listTransfers_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            this.sort((ListView)sender, e);
        }
        private void listEvolves_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            this.sort((ListView)sender, e);
        }
        private void listPokemon_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            this.sort((ListView)sender, e);
        }

        private void disposechooseAmountToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                ListViewItem lvi = listInv.SelectedItems[0];
                string name = lvi.Text;
                string realName = "Item" + name.Replace("Lure", "TroyDisk");
                Type type = typeof(ItemId);
                ItemId item = (ItemId)Enum.Parse(type, realName);
                string amount = Prompt.ShowDialog($"How many {name}s do you want to dispose?", $"Dispose {name}");
                int iAmount = int.Parse(amount);
                Logger.Write($"Trying to dispose {iAmount} {name}s");
                this.disposeItem(item, iAmount);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Unable to dispose item", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void disposeItem(ItemId item, int amount)
        {
            try
            {
                RecycleInventoryItemResponse recResp = await this._session.Client.Inventory.RecycleItem(item, amount);
                Logger.Write($"Disposed {amount} {item}s. Result: {recResp.Result}. You now have {recResp.NewCount} {item}s");
            }
            catch (Exception ex)
            {
                Logger.Write($"disposeItem() failed. {ex.Message}", LogLevel.Error);
            }
        }

        private void kryptonRibbonGroupButton4_Click(object sender, EventArgs e)
        {
            useIncense();
        }

        private void useLuckyEgg_Click(object sender, EventArgs e)
        {
            CancellationToken ct = new CancellationToken();
            Task task = UseLuckyEggConstantlyTask.Execute(this._session, ct);
        }

        private void favoriteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listPokemon.SelectedItems.Count == 1)
            {
                ListViewItem selected = listPokemon.SelectedItems[0];
                string name = selected.SubItems[0].Text;
                ulong id = ulong.Parse(selected.SubItems[10].Text);
                bool isFavorite = selected.SubItems[21].Text == "Yes";
                this.setFavorite(id, !isFavorite);
            }
            else {
                MessageBox.Show($"Unable to favorite multiple pokemon at the same time", $"Sorry...", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        private async void setFavorite(ulong id, bool favorite, bool updateAfter = true)
        {
            try
            {
                SetFavoritePokemonResponse favResp = await this._session.Client.Inventory.SetFavoritePokemon(id, favorite);
                Logger.Write($"Favorited pokemon: {favResp.Result}");

                if (updateAfter)
                {
                    await this.getPokemons();
                }
            }
            catch (Exception ex)
            {
                Logger.Write($"setFavorite() failed. {ex.Message}", LogLevel.Error);
            }
        }

        private void powerUpMAXToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listPokemon.SelectedItems.Count == 1)
            {
                ListViewItem selected = listPokemon.SelectedItems[0];
                string name = selected.SubItems[0].Text;
                ulong id = ulong.Parse(selected.SubItems[10].Text);
                var result = MessageBox.Show($"Are you sure?\nThis will power-up {name} until on of the following occurs:\n - Your amount of stardust is insufficient.\n - You do not have enough candies to power-up.\n - The pokemon cannot be powered-up any further\n - Any other unexpected things that happen along the way.", "Power-Up MAX", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                if (result == DialogResult.OK)
                {
                    this.maxPowerUp(id, name);
                }
            }
            else {
                MessageBox.Show($"Unable to MAX-PowerUp multiple pokemon at the same time", $"Sorry...", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async void maxPowerUp(ulong id, string name)
        {
            try
            {
                int i = 0;
                UpgradePokemonResponse ups = await this._session.Client.Inventory.UpgradePokemon(id);
                while (ups.Result == UpgradePokemonResponse.Types.Result.Success)
                {
                    Logger.Write($"POWERUP-MAX {name} run #{i}. Result: {ups.Result}");
                    i++;
                    DelayingUtils.Delay(this._session.LogicSettings.DelayBetweenPlayerActions, 0);
                    ups = await this._session.Client.Inventory.UpgradePokemon(id);
                }
                Logger.Write($"POWERUP-MAX {name} run #{i}. Result: {ups.Result}. Stopping.");
                await this.getPokemons();
            }
            catch (Exception ex)
            {
                Logger.Write($"maxPowerUp() failed. {ex.Message}", LogLevel.Error);
            }
        }

        private bool isPaused;
        internal void togglePause()
        {
            if (isPaused == true)
            {
                this.unpause();
            }
            else {
                this.pause("");
            }
        }
        internal void pause(string message)
        {
            if (MessageBox.Show(message, "", MessageBoxButtons.OK) == DialogResult.OK)
            {
                Environment.Exit(1);
            }
            //Logger.Write("HOLD! Please restart the program.");
            //isPaused = true;
            //this.UIThread(() => { btnHold.TextLine1 = "Unhold"; });
            //Logger.Write($"Thread: alive: {botThread.IsAlive} with state: {botThread.ThreadState.ToString()}");
            //if (botThread.IsAlive) {
            //    botThread.Abort();
            // }
        }

        internal void unpause()
        {/*
            if (isPaused == false)
            {
                Logger.Write("Continuing..");
                isPaused = false;
                this.UIThread(() => { btnHold.TextLine1 = "Hold"; });
                //waitHandle.Set();
                botThread.Resume();
            }
            else {
                Logger.Write("Not held up...");
            }*/
        }

        private void btnHold_Click(object sender, EventArgs e)
        {
            this.togglePause();
        }

        private void btnMapReload_Click(object sender, EventArgs e)
        {
            btnMapReload.Checked = ButtonCheckState.Checked;
            webMap.Refresh();
        }
    }




    public static class KryptonFormExtension
    {
        static public void UIThread(this KryptonForm form, MethodInvoker code)
        {
            if (form.InvokeRequired)
            {
                form.Invoke(code);
                return;
            }
            code.Invoke();
        }
    }

    public static class RichTextBoxExtensions
    {
        public static void AppendText(this RichTextBox box, string text, Color color)
        {
            box.SelectionStart = box.TextLength;
            box.SelectionLength = 0;

            box.SelectionColor = color;
            box.AppendText(text);
            box.SelectionColor = box.ForeColor;
        }
    }

    public static class Prompt
    {
        public static string ShowDialog(string text, string caption)
        {
            Form prompt = new Form()
            {
                Width = 500,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = caption,
                StartPosition = FormStartPosition.CenterScreen
            };
            Label textLabel = new Label() { Left = 50, Top = 20, Text = text, Width = 400 };
            TextBox textBox = new TextBox() { Left = 50, Top = 50, Width = 400 };
            Button confirmation = new Button() { Text = "Ok", Left = 350, Width = 100, Top = 70, DialogResult = DialogResult.OK };
            confirmation.Click += (sender, e) => { prompt.Close(); };
            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.Controls.Add(textLabel);
            prompt.AcceptButton = confirmation;

            return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : null;
        }
    }

}
