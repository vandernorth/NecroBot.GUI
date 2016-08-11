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
using POGOProtos.Map.Fort;
using POGOProtos.Networking.Responses;
using System;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PoGo.NecroBot.GUI
{

    public partial class PokeGUI : KryptonForm
    {
        private DragManager _dm;
        private bool mapLoaded;
        private Session _session;
        private ListViewColumnSorter lvwColumnSorter;
        private int pokemonCaught;
        private int pokestopsVisited;

        public PokeGUI()
        {
            this.mapLoaded = false;
            this.pokemonCaught = 0;
            this.pokestopsVisited = 0;
            InitializeComponent();
            doComponentSettings();
            startUp();
        }

        private void doComponentSettings() {
            _dm = new DragManager();
            _dm.StateCommon.Feedback = PaletteDragFeedback.Block;
            _dm.DragTargetProviders.Add(workspaceDashboard);
            workspaceDashboard.DragPageNotify = _dm;
            workspaceDashboard.ShowMaximizeButton = true;
            lvwColumnSorter = new ListViewColumnSorter();
            this.listPokemon.ListViewItemSorter = lvwColumnSorter;
            this.listPokemonStats.Items.Clear();
            fixWebMap();
        }

        private void fixWebMap() {
            try {
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
            catch (Exception ex) {
                Logger.Write("Unable to add registry key: " + ex.Message, LogLevel.Error);
            }
        }

        private void workspaceDashboard_WorkspaceCellAdding(object sender, WorkspaceCellEventArgs e)
        {
            e.Cell.NavigatorMode = NavigatorMode.BarRibbonTabGroup;
            e.Cell.Button.CloseButtonDisplay = ButtonDisplay.Hide;
            e.Cell.Button.ContextButtonDisplay = ButtonDisplay.Logic;
        }

        delegate void SetTextCallback(string text);
        delegate void SetTextControl(string text, Control control, bool keep = false, bool addTime = false, string modify = "Text");
        delegate void SetLocationDelegate(double lang, double lat);
        delegate void SetPokestopDelegate(string type, string id, string lng, string lat, string name, string extra);
        delegate void SetListDelegate(ListView control, ListViewItem[] lvi);

        public void addPokemonCaught() {
            this.pokemonCaught++;
            this.updateStatusCounters();
        }
        public void addPokestopVisited()
        {
            this.pokestopsVisited++;
            this.updateStatusCounters();
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
            if (control.InvokeRequired)
            {
                SetTextControl d = new SetTextControl(SetControlText);
                this.Invoke(d, new object[] { text, control, keep, addTime, modify });
            }
            else
            {
                if (addTime)
                {
                    text = $"[{DateTime.Now.ToString("HH:mm:ss")}] {text}";
                }


                if (keep)
                {
                    if (control.Text == "..." || control.Text == ".") {
                        control.Text = "";
                    }
                    control.Text = text + "\n" + control.Text;
                }
                else {
                    control.GetType().GetProperty(modify).SetValue(control, text, null);
                }
            }
        }

        private void SetList(ListView control, ListViewItem[] lvi)
        {
            if (control.InvokeRequired)
            {
                SetListDelegate d = new SetListDelegate(SetList);
                this.Invoke(d, new object[] { control, lvi });
            }
            else
            {
                control.Items.Clear();
                foreach (var item in lvi)
                {
                    control.Items.Add(item);
                }
            }
        }

        private void SetText(string text)
        {
            this.UIThread(() => {
                this.textLog.AppendText(text);
                this.textLog.AppendText(Environment.NewLine);
                this.textLog.SelectionStart = this.textLog.Text.Length;
                this.textLog.ScrollToCaret();
            });
        }

        private void setLocation(double lang, double lat)
        {
            if (this.webMap.InvokeRequired)
            {
                SetLocationDelegate d = new SetLocationDelegate(setLocation);
                this.Invoke(d, new object[] { lang, lat });
            }
            else
            {
                if (this.webMap.Document != null && this.mapLoaded == true)
                {
                    Object[] objArray = new Object[2];
                    objArray[0] = (Object)lat;
                    objArray[1] = (Object)lang;
                    this.webMap.Document.InvokeScript("updateMarker", objArray);
                }
            }
        }

        private void setFort(string type, string id, string lng, string lat, string name, string extra)
        {
            if (this.webMap.InvokeRequired)
            {
                SetPokestopDelegate d = new SetPokestopDelegate(setFort);
                this.Invoke(d, new object[] { type, id, lng, lat, name, extra });
            }
            else
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
            }
        }

        private void setLogger() {
            Write writes = (string message, LogLevel level, ConsoleColor color) => {

                if (level == LogLevel.Pokestop)
                {
                    if (message.Contains("Arriving"))
                    {
                        this.SetControlText(message.Replace("Arriving to Pokestop:", "Next stop:"), this.labelNext, false, false);
                    }
                    else {
                        string[] newMessage = message.Replace("Name:", "").Replace("XP", "*").Replace(", Lat", "*").Split('*');
                        this.SetControlText($"{newMessage[0].PadRight(35, ' ')} XP{newMessage[1]}", this.labelStops, true, true);
                    }

                }
                else if (level == LogLevel.Caught)
                {
                    this.SetControlText(message, this.labelPokemon, true, true);
                }
                else if (level == LogLevel.Evolve)
                {
                    this.SetControlText(message, this.labelEvolves, true, true);
                }
                else if (level == LogLevel.Transfer)
                {
                    this.SetControlText(message, this.labelTransfers, true, true);
                }
                else if (level == LogLevel.Info && message.Contains("Playing"))
                {
                    this.onceLoaded();
                }
                else if (level == LogLevel.Info && message.Contains("Amount of Pokemon Caught"))
                {
                    Regex regex = new Regex(@"Caught:");
                    this.UIThread(() => labelPokemonAmount.TextLine2 = regex.Split(message)[1]);
                }
                else {
                    this.SetText($"[{DateTime.Now.ToString("HH:mm:ss")}] ({level.ToString()}) {message}");
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
                Thread.Sleep(2000);
                Environment.Exit(0);
                return;
            }
            else {
                settings.TranslationLanguageCode = "en";
                settings.AutoUpdate = false;
            }
            var session = new Session(new ClientSettings(settings), new LogicSettings(settings));
            _session = session;
            session.Client.ApiFailure = new ApiFailureStrategy(session);

            var machine = new StateMachine();
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
            machine.SetFailureState(new LoginState());
            Logger.SetLoggerContext(session);

            session.Navigation.UpdatePositionEvent += (lat, lng) => {
                session.EventDispatcher.Send(new UpdatePositionEvent { Latitude = lat, Longitude = lng });
                this.UIThread(delegate
                {
                    this.labelLat.TextLine2 = lat.ToString("0.00000");
                    this.labelLong.TextLine2 = lng.ToString("0.00000");
                });
                this.setLocation(lng, lat);
            };

            machine.AsyncStart(new VersionCheckState(), session);

            string filename = Application.StartupPath + $"\\Map\\getMap.html?lat={settings.DefaultLatitude}&long={settings.DefaultLongitude}&radius={settings.MaxTravelDistanceInMeters}";
            this.webMap.ScriptErrorsSuppressed = true;
            this.webMap.Url = new Uri(filename);
            
            if (settings.UseTelegramAPI)
            {
                session.Telegram = new Logic.Service.TelegramService(settings.TelegramAPIKey, session);
            }

            if (session.LogicSettings.UseSnipeLocationServer)
            {
                SnipePokemonTask.AsyncStart(session);
                //CancellationToken cancellationToken = new CancellationToken();
                //SnipePokemonTask.Execute(session, cancellationToken);
            }

            columnExtra.Width = 0;
            systemId.Width = 0;

        }

        private void onceLoaded()
        {
            this.runUpdate();
            this.setLocation(this._session.Settings.DefaultLongitude, this._session.Settings.DefaultLatitude);
        }

        private void webMap_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            this.mapLoaded = true;
            this.webMap.ScriptErrorsSuppressed = true;
        }

        private async void runUpdate()
        {
            await this._session.Inventory.RefreshCachedInventory();
            await this.getInventory();
            await this.getPokemons();
            this.getEggs();
            this.getForts();
        }

        private async void getForts()
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

        public void updateIncubator(string id, string kmRemaining) {
            this.UIThread(() => {
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

            this.UIThread(() => {
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
                    }

                }
            });
        }
        private async Task getPokemons()
        {
            var items = await this._session.Inventory.GetPokemons();
            var myPokemonSettings = await this._session.Inventory.GetPokemonSettings();
            var pokemonSettings = myPokemonSettings.ToList();
            var myPokemonFamilies = await this._session.Inventory.GetPokemonFamilies();
            var pokemonFamilies = myPokemonFamilies.ToArray();
            var pokemonPairedWithStatsCp = items.Select(pokemon => Tuple.Create(pokemon, PokemonInfo.CalculateMaxCp(pokemon), PokemonInfo.CalculatePokemonPerfection(pokemon), PokemonInfo.GetLevel(pokemon))).ToList();

            bool[] pokemonsIHave = new bool[152];
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
                var timeSpan = this.FromUnixTime(item.Item1.CreationTimeMs / 1000);
                var localDateTime = timeSpan.ToString("yyyy-MM-dd HH:mm:ss");
                string[] row = {thisName, ((int)(item.Item1.PokemonId)).ToString(), cpInfo, item.Item2.ToString("F0"), cpPrcnt, item.Item3.ToString("F0") + "%", item.Item4.ToString(), canEvolve, thisCandies, extra, item.Item1.Id.ToString(), item.Item1.Move1.ToString(), item.Item1.Move2.ToString(), localDateTime,
                            $"{item.Item1.HeightM:0.00}m", $"{item.Item1.WeightKg:0.00}kg", $"{item.Item1.Stamina}/{item.Item1.StaminaMax}", item.Item1.NumUpgrades.ToString(), item.Item1.IndividualAttack.ToString(), item.Item1.IndividualDefense.ToString(), item.Item1.IndividualStamina.ToString(), isFavorite, item.Item1.BattlesAttacked.ToString(),item.Item1.BattlesDefended.ToString(),item.Item1.DeployedFortId  };
                ListViewItem thisOne = new ListViewItem(row);
                thisOne.ImageIndex = (int)(item.Item1.PokemonId);
                pokemonsIHave[(int)(item.Item1.PokemonId)] = true;
                lvis[index] = thisOne;
                index++;
            }
            Logger.Write($"Adding {lvis.Count()} to list");
            this.SetList(listPokemon, lvis);

            this.UIThread(()=> {
                this.listPokemonStats.Clear();
                for (int i = 1; i < 152; i++) {
                    if (pokemonsIHave[i] != true) {
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

        public DateTime FromUnixTime(ulong unixTime)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddSeconds(unixTime);
        }
        private async Task getInventory()
        {
            var items = await this._session.Inventory.GetItems();
            try
            {
                ListViewItem[] lvis = new ListViewItem[items.Count()];
                int index = 0;
                foreach (var item in items)
                {
                    POGOProtos.Inventory.Item.ItemType itemType = (POGOProtos.Inventory.Item.ItemType)item.ItemId;
                    string[] row = { item.ItemId.ToString().Replace("Item", "").Replace("TroyDisk", "Lure"), item.Count.ToString(), "" };
                    ListViewItem thisOne = new ListViewItem(row);
                    lvis[index] = thisOne;
                    index++;
                }
                this.SetList(listInv, lvis);
            }
            catch (Exception e)
            {
                Logger.Write("Error" + e.Message, LogLevel.Error);
            }
        }

        private async void useIncense()
        {
            var incense = await this._session.Client.Inventory.UseIncense(POGOProtos.Inventory.Item.ItemId.ItemIncenseOrdinary);
            Logger.Write($"Incense result: {incense.Result} item: {incense.AppliedIncense}", LogLevel.Info);
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

        private void listPokemon_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            // Determine if clicked column is already the column that is being sorted.
            if (e.Column == lvwColumnSorter.SortColumn)
            {
                // Reverse the current sort direction for this column.
                if (lvwColumnSorter.Order == SortOrder.Ascending)
                {
                    lvwColumnSorter.Order = SortOrder.Descending;
                }
                else
                {
                    lvwColumnSorter.Order = SortOrder.Ascending;
                }
            }
            else
            {
                // Set the column number that is to be sorted; default to ascending.
                lvwColumnSorter.SortColumn = e.Column;
                lvwColumnSorter.Order = SortOrder.Ascending;
            }

            // Perform the sort with these new sort options.
            this.listPokemon.Sort();
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
                    for (int i = 0; i < listPokemon.SelectedItems.Count; i++) {
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
            ReleasePokemonResponse rps = await this._session.Client.Inventory.TransferPokemon(id);
            MessageBox.Show($"Transfer result: {rps.Result}\nCandies retrieved: {rps.CandyAwarded.ToString()}", "Transfer result", MessageBoxButtons.OK, MessageBoxIcon.Information);
            if (runUpdate) { await this.getPokemons(); }
            
        }

        private async void transferMultiple(ulong[] ids)
        {
            foreach (ulong id in ids) {
                ReleasePokemonResponse rps = await this._session.Client.Inventory.TransferPokemon(id);
                Logger.Write($"Transfer result: {rps.Result} - Candies retrieved: {rps.CandyAwarded.ToString()} id: {id.ToString()}", LogLevel.Transfer);
                Logger.Write($"Transfer result: {rps.Result} - Candies retrieved: {rps.CandyAwarded.ToString()} id: {id.ToString()}");
                DelayingUtils.Delay(this._session.LogicSettings.DelayBetweenPlayerActions, 0);
            }
            await this.getPokemons();
            Logger.Write($"Transfer of {ids.Length} pokemons completed");
        }

        private async void evolveMultiple(ulong[] ids)
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

        private async void evolvePokemon(ulong id, bool runUpdate = true)
        {
            EvolvePokemonResponse eps = await this._session.Client.Inventory.EvolvePokemon(id);
            MessageBox.Show($"Evolve result: {eps.EvolvedPokemonData.PokemonId} CP: {eps.EvolvedPokemonData.Cp}\nXP: {eps.ExperienceAwarded.ToString()}", "Evolve result", MessageBoxButtons.OK, MessageBoxIcon.Information);
            if (runUpdate) { await this.getPokemons(); }
        }

        private async void powerUp(ulong id, bool runUpdate = true)
        {
            UpgradePokemonResponse ups = await this._session.Client.Inventory.UpgradePokemon(id);
            MessageBox.Show($"PowerUp result: {ups.Result}\nCP: {ups.UpgradedPokemon.Cp}", $"PowerUp result for {ups.UpgradedPokemon.PokemonId.ToString()}", MessageBoxButtons.OK, MessageBoxIcon.Information);
            if (runUpdate) { await this.getPokemons(); }
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            Task task = new Task(runUpdate);
            task.Start();
        }

        private void kryptonRibbonGroupButton13_Click(object sender, EventArgs e)
        {

        }

        private void kryptonRibbonGroup5_DialogBoxLauncherClick(object sender, EventArgs e)
        {

        }

        private void snipeButton_Click(object sender, EventArgs e)
        {
            SnipePokemonTask.AsyncStart(this._session);
           
        }

        private void snipeButton2_Click(object sender, EventArgs e)
        {
            CancellationToken cancellationToken = new CancellationToken();
            SnipePokemonTask.Execute(this._session, cancellationToken);
        }
    }

    public static class KryptonFormExtension {
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
}
