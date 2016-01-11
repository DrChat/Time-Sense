﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Database;
using Microsoft.ApplicationInsights.DataContracts;
using Stuff;
using Windows.Data.Xml.Dom;
using Windows.Media.SpeechSynthesis;
using Windows.UI.Notifications;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.Storage.Pickers;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.ViewManagement;

namespace Time_Sense
{
    public sealed partial class HomePage : Page
    {
        static DateTime[] date = new DateTime[2];
        public List<Hour> hour_list_raw = new List<Hour>();

        MediaElement m_element = new MediaElement();

        public class ts_data
        {
            public string usage { get; set; }
            public string unlocks { get; set; }
            public string perc_str { get; set; }
            public int perc { get; set; }
            public string usage_max { get; set; }
            public string usage_min { get; set; }
            public string usage_avg { get; set; }
            public string unlocks_max { get; set; }
            public string unlocks_min { get; set; }
            public string unlocks_avg { get; set; }
            public string battery_usage { get; set; }
            public string battery_unlocks { get; set; }
            public Visibility ch_panel { get; set; }
            public Visibility notch_panel { get; set; }
            public List<Hour> h_list { get; set; }
            public Visibility note_panel { get; set; }
            public Visibility nonote_panel { get; set; }
            public string note { get; set; }
        }

        static int[] time = new int[2];
        static int[] unlocks = new int[2];
        static int[] total_seconds = new int[2];
        static int diff = 0;

        public HomePage()
        {
            this.InitializeComponent();
            InputPane.GetForCurrentView().Showing +=
                (s, args) => { bottom_bar.Visibility = Visibility.Collapsed; };
            InputPane.GetForCurrentView().Hiding += (s, args2) =>
            {
                if (bottom_bar.Visibility == Visibility.Collapsed)
                {
                    bottom_bar.Visibility = Visibility.Visible;
                }
            };
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            App.t_client.TrackPageView("Home page");
            ButtonSwitch(false);
            refresh();
            ShowData();
            if (App.jump_arguments == "usage")
            {
                SpeechSynthesizer syntetizer = new SpeechSynthesizer();
                int[] data = utilities.SplitData(time[1]);
                SpeechSynthesisStream stream = await syntetizer.SynthesizeTextToStreamAsync(String.Format(utilities.loader.GetString("voice_usage"), data[0], data[0] == 1 ? utilities.loader.GetString("hour") : utilities.loader.GetString("hours"), data[1], data[1] == 1 ? utilities.loader.GetString("minute") : utilities.loader.GetString("minutes"), data[2], data[2] == 1 ? utilities.loader.GetString("second") : utilities.loader.GetString("seconds"), unlocks[1], unlocks[1] == 1 ? utilities.loader.GetString("time") : utilities.loader.GetString("times")));
                m_element.SetSource(stream, stream.ContentType);
                m_element.Play();
                MainPage.parameter = null;
            }
            App.jump_arguments = null;
            ButtonSwitch(true);
        }

        private async void refresh_home(object sender, RoutedEventArgs e)
        {
            ButtonSwitch(false);
            App.t_client.TrackEvent("Usage refreshed");
            refresh();
            ShowData();
            ButtonSwitch(true);
        }

        public static async void refresh()
        {
            if (App.report_date.Date == DateTime.Now.Date)
            {
                date[1] = DateTime.Now;
                if (utilities.CheckDate(settings.date))
                {
                    for (int i = 0; i < 2; i++)
                    {
                        time[i] = 0;
                        unlocks[i] = 0;
                    }
                    try
                    {
                        date[0] = DateTime.Parse(utilities.STATS.Values[settings.date].ToString());
                    }
                    catch
                    {
                        goto save;
                    }
                    // LOAD TIME FROM DATABASE
                    //IF THE DAY HASN'T CHANGED, LOAD ONLY ONE COUPLE OF DATA
                    await Helper.InitializeDatabase();
                    for (int i = date[0].Date == date[1].Date ? 1 : 0; i < 2; i++)
                    {
                        string date_str = utilities.shortdate_form.Format(date[i]);
                        var item = await Helper.ConnectionDb().Table<Report>().Where(x => x.date == date_str).FirstOrDefaultAsync();
                        time[i] = item == null ? 0 : item.usage;
                        var unlocks_list = await Helper.ConnectionDb().Table<Timeline>().Where(x => x.date == date_str).ToListAsync();
                        unlocks[i] = unlocks_list.Count;
                    }
                    //END LOADING
                    ConvertSeconds();
                    if (date[0].Year != date[1].Year)
                    {
                        if (date[0].Day == 31 && date[1].Day == 1)
                        {
                            time[0] += (86400 - total_seconds[0]);
                            time[1] += total_seconds[1];
                            await Helper.UpdateHourItem(date[0], date[0].Hour, (((date[0].Hour + 1) * 3600) - total_seconds[0]), 0);
                            for (int i = 1; i < 24 - date[0].Hour; i++)
                            {
                                await Helper.UpdateHourItem(date[0], date[0].Hour + i, 3600, 0);
                            }
                            for (int i = 0; i < date[1].Hour; i++)
                            {
                                await Helper.UpdateHourItem(date[1], i, 3600, i == 0 ? 1 : 0);
                            }
                            await Helper.UpdateHourItem(date[1], date[1].Hour, total_seconds[1] - (date[1].Hour * 3600), 0);
                            for (int i = 0; i < 2; i++)
                            {
                                await Helper.UpdateUsageItem(time[i], unlocks[i], date[i]);
                            }
                            await Helper.UpdateTimelineItem(unlocks[0], (86400 - total_seconds[0]), date[0]);
                            var radios = await Windows.Devices.Radios.Radio.GetRadiosAsync();
                            var bluetooth_device = radios.Where(x => x.Kind == Windows.Devices.Radios.RadioKind.Bluetooth).FirstOrDefault();
                            var wifi_device = radios.Where(x => x.Kind == Windows.Devices.Radios.RadioKind.WiFi).FirstOrDefault();
                            string bluetooth = bluetooth_device == null ? "off" : bluetooth_device.State == Windows.Devices.Radios.RadioState.On ? "on" : "off";
                            string wifi = wifi_device == null ? "off" : wifi_device.State == Windows.Devices.Radios.RadioState.On ? "on" : "off";
                            string battery = Windows.Devices.Power.Battery.AggregateBattery.GetReport().Status == Windows.System.Power.BatteryStatus.Charging ? "charging" : "null";
                            await Helper.AddTimelineItem(date[1], "00:00:00", 1, Windows.System.Power.PowerManager.RemainingChargePercent, battery, bluetooth, wifi);
                            await Database.Helper.UpdateTimelineItem(1, time[1], date[1]);
                        }
                    }
                    else
                    {
                        if (date[0].Date == date[1].Date)
                        {
                            time[1] += diff;
                            if (date[0].Hour < date[1].Hour)
                            {
                                await Helper.UpdateHourItem(date[1], date[0].Hour, ((date[0].Hour + 1) * 3600) - total_seconds[0], 0);
                                for (int i = 1; i < date[1].Hour - date[0].Hour; i++)
                                {
                                    await Helper.UpdateHourItem(date[1], date[0].Hour + i, 3600, 0);
                                }
                                await Helper.UpdateHourItem(date[1], date[1].Hour, total_seconds[1] - (date[1].Hour * 3600), 0);
                            }
                            else if (date[0].Hour == date[1].Hour)
                            {
                                await Helper.UpdateHourItem(date[1], date[1].Hour, diff, 0);
                            }
                            await Helper.UpdateUsageItem(time[1], unlocks[1], date[1]);
                            await Helper.UpdateTimelineItem(unlocks[1], diff, date[1]); //AGGIORNA TIMELINE
                        }
                        else if (date[1].DayOfYear - date[0].DayOfYear == 1)
                        {
                            time[0] += (86400 - total_seconds[0]);
                            time[1] += total_seconds[1];
                            await Helper.UpdateHourItem(date[0], date[0].Hour, (((date[0].Hour + 1) * 3600) - total_seconds[0]), 0);
                            for (int i = 1; i < 24 - date[0].Hour; i++)
                            {
                                await Helper.UpdateHourItem(date[0], date[0].Hour + i, 3600, 0);
                            }
                            for (int i = 0; i < date[1].Hour; i++)
                            {
                                await Helper.UpdateHourItem(date[1], i, 3600, i == 0 ? 1 : 0);
                            }
                            await Helper.UpdateHourItem(date[1], date[1].Hour, total_seconds[1]- (date[1].Hour * 3600), 0);
                            for (int i = 0; i < 2; i++)
                            {
                                await Helper.UpdateUsageItem(time[i], unlocks[i], date[i]);
                            }
                            await Helper.UpdateTimelineItem(unlocks[0], (86400 - total_seconds[0]), date[0]);
                            var radios = await Windows.Devices.Radios.Radio.GetRadiosAsync();
                            var bluetooth_device = radios.Where(x => x.Kind == Windows.Devices.Radios.RadioKind.Bluetooth).FirstOrDefault();
                            var wifi_device = radios.Where(x => x.Kind == Windows.Devices.Radios.RadioKind.WiFi).FirstOrDefault();
                            string bluetooth = bluetooth_device == null ? "off" : bluetooth_device.State == Windows.Devices.Radios.RadioState.On ? "on" : "off";
                            string wifi = wifi_device == null ? "off" : wifi_device.State == Windows.Devices.Radios.RadioState.On ? "on" : "off";
                            string battery = Windows.Devices.Power.Battery.AggregateBattery.GetReport().Status == Windows.System.Power.BatteryStatus.Charging ? "charging" : "null";
                            await Helper.AddTimelineItem(date[1], "00:00:00", 1, Windows.System.Power.PowerManager.RemainingChargePercent, battery, bluetooth, wifi);
                            await Database.Helper.UpdateTimelineItem(1, time[1], date[1]);
                        }
                    }
                }
                save:
                utilities.STATS.Values[settings.date] = date[1].ToString();
            }
        }

        public async void ShowData()
        {
            if (App.report_date.Date != DateTime.Now.Date)
            {
                int[] usage = await Helper.LoadReportItem(App.report_date);
                time[1] = usage[0];
                unlocks[1] = usage[1];
            }
            hour_list_raw = await Helper.GetHourList(App.report_date);
            List<Hour> hour_list = new List<Hour>();

            double time_helper = 0;
            double unlocks_helper = 0;
            double time_min_helper = 1000;
            double unlocks_min_helper = 1000;
            double time_total = 0;
            double unlocks_total = 0;
            double avg_t = 0;
            double avg_u = 0;
            foreach (var item in hour_list_raw)
            {
                item.usage = item.usage / 60;
                if (item.usage != 0)
                {
                    if (item.usage > time_helper)
                    {
                        time_helper = item.usage;
                    }
                    if (item.usage < time_min_helper && item.usage != 0)
                    {
                        time_min_helper = item.usage;
                    }
                    avg_t++;
                }
                if (item.unlocks != 0)
                {
                    if (item.unlocks > unlocks_helper)
                    {
                        unlocks_helper = item.unlocks;
                    }
                    if (item.unlocks < unlocks_min_helper && item.unlocks != 0)
                    {
                        unlocks_min_helper = item.unlocks;
                    }
                    avg_u++;
                }
                time_total += item.usage;
                unlocks_total += item.unlocks;
                hour_list.Add(item);
            }

            ts_data ts = new ts_data();

            ts.usage_max = time_helper == 0 ? "---" : time_helper.ToString();
            ts.unlocks_max = unlocks_helper == 0 ? "---" : unlocks_helper.ToString();
            ts.usage_min = time_min_helper == 1000 ? "---" : time_min_helper.ToString();
            ts.unlocks_min = unlocks_min_helper == 1000 ? "---" : unlocks_min_helper.ToString();
            ts.usage_avg = Math.Round((time_total / avg_t), 2).ToString();
            ts.unlocks_avg = Math.Round((unlocks_total / avg_u), 2).ToString();
            ts.usage = FormatData(time[1]);
            ts.unlocks = unlocks[1] == 1 ? String.Format(utilities.loader.GetString("unlock"), unlocks[1]) : String.Format(utilities.loader.GetString("unlocks"), unlocks[1]);
            ts.perc_str = ((time[1] * 100) / 86400).ToString() + "%";
            ts.perc = time[1];
            ts.h_list = hour_list;

            //LOAD NOTE
            string date_str = utilities.shortdate_form.Format(App.report_date);
            var report = await Helper.ConnectionDb().Table<Report>().Where(x => x.date == date_str).FirstOrDefaultAsync();
            if (report != null)
            {
                ts.note = report.note == null ? "" : report.note;
                ts.nonote_panel = report.note == null || report.note == "" ? Visibility.Visible : Visibility.Collapsed;
                ts.note_panel = report.note == null || report.note == "" ? Visibility.Collapsed : Visibility.Visible;
            }
            else
            {
                ts.note = "";
                ts.nonote_panel = Visibility.Visible;
                ts.note_panel = Visibility.Collapsed;
            }

            //BATTERY DATA
            if (Windows.Devices.Power.Battery.AggregateBattery.GetReport().Status == Windows.System.Power.BatteryStatus.Charging)
            {
                ts.notch_panel = Visibility.Collapsed;
                ts.ch_panel = Visibility.Visible;
            }
            else
            {
                ts.notch_panel = Visibility.Visible;
                ts.ch_panel = Visibility.Collapsed;
                var list = await Helper.ConnectionDb().Table<Timeline>().ToListAsync();
                int list_count = list.Count - 1;
                int batt_time = 0;
                int battery = 0;
                int batt_unlocks = 0;
                if (list_count > 0)
                {
                    do
                    {
                        try
                        {
                            var item = list.ElementAt(list_count);
                            if (item != null && item.battery_status != "charging")
                            {
                                batt_time += int.Parse(item.usage.ToString());
                                batt_unlocks++;
                                battery = item.battery;
                            }
                        }
                        catch (Exception e)
                        {
                            App.t_client.TrackException(new ExceptionTelemetry(e));
                        }
                        finally
                        {
                            list_count--;
                        }
                    }
                    while (CheckBattery(list, battery, list_count));
                }
                else
                {
                    batt_time = time[1];
                    batt_unlocks = unlocks[1];
                }
                ts.battery_usage = FormatData(batt_time);
                ts.battery_unlocks = batt_unlocks == 1 ? String.Format(utilities.loader.GetString("unlock"), batt_unlocks) : String.Format(utilities.loader.GetString("unlocks"), batt_unlocks);
            }

            if (App.report_date.Date == DateTime.Now.Date)
            {
                UpdateTile();
            }

            await Task.Delay(1);
            MainPage.title.Text = App.report_date.Date == DateTime.Now.Date ? utilities.loader.GetString("today") : App.report_date.Date == DateTime.Now.Subtract(new TimeSpan(1, 0, 0, 0, 0)).Date ? utilities.loader.GetString("yesterday") : utilities.shortdate_form.Format(App.report_date);
            this.DataContext = ts;
        }

        private bool CheckBattery(List<Timeline> list, int battery, int count )
        {
            count = count--;
            if (count >= 0)
            {
                var item = list.ElementAt(count);
                if (item != null)
                {
                    if (item.battery < battery || item.battery_status == "charging") { return false; }
                    else { return true; }
                } else { return false; }
            } else { return false; }
        }

        private void UpdateTile()
        {
            // UPDATE THE TILE
            bool badge = utilities.STATS.Values[settings.unlocks] == null ? true : utilities.STATS.Values[settings.unlocks].ToString() == "badge" ? true : false;
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(utilities.TileXmlBuilder(utilities.FormatData(time[1]), unlocks[1], badge));
            TileNotification tile = new TileNotification(doc);
            TileUpdateManager.CreateTileUpdaterForApplication().Update(tile);
            if (badge)
            {
                string badgeXmlString = "<badge value='" + unlocks[1].ToString() + "'/>";
                XmlDocument badgeDOM = new XmlDocument();
                badgeDOM.LoadXml(badgeXmlString);
                BadgeNotification badge_not = new BadgeNotification(badgeDOM);
                BadgeUpdateManager.CreateBadgeUpdaterForApplication().Update(badge_not);
            }
        }

        private static void ConvertSeconds()
        {
            for (int i = 0; i < 2; i++)
            {
                total_seconds[i] = (date[i].Hour * 3600) + (date[i].Minute * 60) + date[i].Second;
            }
            diff = total_seconds[1] - total_seconds[0];
        }

        private string FormatData(int usage)
        {
            int hour = usage / 3600;
            int minutes = (usage - (hour * 3600)) / 60;
            int seconds = (usage - (hour * 3600)) - (minutes * 60);
            string letter = utilities.STATS.Values[settings.letters] == null ? "{0}:{1}:{2}" : "{0}h:{1}m:{2}s";
            return String.Format(letter, hour, minutes, seconds);
        }

        private async void date_report_bar_Click(object sender, RoutedEventArgs e)
        {
            ButtonSwitch(false);
            App.t_client.TrackEvent("Calendar shown");
            if (await new DateDialog().ShowAsync() == ContentDialogResult.Primary)
            {
                if (App.report_date.Date == DateTime.Now.Date) { refresh(); }
                ShowData();
            }
            ButtonSwitch(true);
        }

        private async void back_report_bar_Click(object sender, RoutedEventArgs e)
        {
            ButtonSwitch(false);
            App.t_client.TrackEvent("Previous day");
            App.report_date = App.report_date.Subtract(new TimeSpan(1, 0, 0, 0));
            if (App.report_date.Date == DateTime.Now.Date) { refresh(); }
            ShowData();
            ButtonSwitch(true);
        }

        private async void forward_report_bar_Click(object sender, RoutedEventArgs e)
        {
            ButtonSwitch(false);
            App.t_client.TrackEvent("Next day");
            App.report_date = App.report_date.AddDays(1);
            if (App.report_date.Date == DateTime.Now.Date) { refresh(); }
            ShowData();
            ButtonSwitch(true);
        }

        private void home_charts_pivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            App.t_client.TrackEvent("Home pivot swipe");
            try
            {
                switch (home_charts_pivot.SelectedIndex)
                {
                    case 0:
                        home_chart_helper.Text = utilities.loader.GetString("usage_hour_title");
                        break;
                    case 1:
                        home_chart_helper.Text = utilities.loader.GetString("unlocks_hour_title");
                        break;
                    case 3:
                        home_chart_helper.Text = utilities.loader.GetString("charge_banner");
                        break;
                    case 2:
                        home_chart_helper.Text = utilities.loader.GetString("note_banner");
                        break;
                }
            }
            catch { }
        }

        public void ButtonSwitch(bool enabled)
        {
            bottom_bar.IsEnabled = enabled;
        }

        private async void note_edit_btn_Click(object sender, RoutedEventArgs e)
        {
            SymbolIcon icon = note_edit_btn.Icon as SymbolIcon;
            if(icon.Symbol  == Symbol.Edit)
            {
                //EDIT NOTE
                App.t_client.TrackEvent("Edit note");
                note_txt.Visibility = Visibility.Visible;
                note_edit_btn.Icon = new SymbolIcon(Symbol.Accept);
                note_txt.IsReadOnly = false;
                note_txt.Focus(FocusState.Programmatic);
                no_note_txt.Visibility = Visibility.Collapsed;
            }
            else
            {
                //SAVE NOTE
                string date_str = utilities.shortdate_form.Format(App.report_date);
                var report = await Database.Helper.ConnectionDb().Table<Report>().Where(x => x.date == date_str).FirstOrDefaultAsync();
                if(report != null)
                {
                    //DAY XIST IN DB, SAV
                    App.t_client.TrackEvent("Save note");
                    report.note = note_txt.Text;
                    no_note_txt.Visibility = Visibility.Collapsed;
                    await Helper.ConnectionDb().UpdateAsync(report);
                }
                else
                {
                    await new MessageDialog(utilities.loader.GetString("no_note"), utilities.loader.GetString("error")).ShowAsync();
                    note_txt.Text = "";
                }
                no_note_txt.Visibility = string.IsNullOrEmpty(note_txt.Text) ? Visibility.Visible : Visibility.Collapsed;
                note_txt.Visibility = string.IsNullOrEmpty(note_txt.Text) ? Visibility.Collapsed : Visibility.Visible;
                note_edit_btn.Icon = new SymbolIcon(Symbol.Edit);
                note_txt.IsReadOnly = true;

            }
        }

        private async void export_bar_Click(object sender, RoutedEventArgs e)
        {
            try {
                var span_result = await new SpanDialog().ShowAsync();
                if (span_result == Windows.UI.Xaml.Controls.ContentDialogResult.Primary)
                {
                    if (App.range_start_date <= App.range_end_date)
                    {
                        FileSavePicker export_picker = new FileSavePicker();
                        export_picker.DefaultFileExtension = ".xlsx";
                        export_picker.SuggestedFileName = String.Format("timesense_{0}-{1}", utilities.shortdate_form.Format(App.range_start_date), utilities.shortdate_form.Format(App.range_end_date));
                        export_picker.FileTypeChoices.Add("Excel file", new List<string>() { ".xlsx" });
                        App.file_pick = true;
                        StorageFile export_file = await export_picker.PickSaveFileAsync();
                        if (export_file != null)
                        {
                            App.file_pick = false;
                            await new ProgressDialog(export_file).ShowAsync();
                            App.t_client.TrackEvent("Excel report created");
                        }
                    }
                    else
                    {
                        await new MessageDialog(utilities.loader.GetString("error_span"), utilities.loader.GetString("error")).ShowAsync();
                    }
                }
            }
            catch { }
        }
    }
}
