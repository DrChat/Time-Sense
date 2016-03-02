﻿using Database;
using GalaSoft.MvvmLight.Command;
using Microsoft.ApplicationInsights.DataContracts;
using Stuff;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Template10.Mvvm;
using Windows.Media.SpeechSynthesis;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Time_Sense.ViewModels
{
    public class HomeViewModel : ViewModelBase
    {
        public class Records
        {
            public int usage_max { get; set; }
            public int usage_min { get; set; }
            public int usage_avg { get; set; }
            public int unlocks_max { get; set; }
            public int unlocks_min { get; set; }
            public int unlocks_avg { get; set; }
            public int battery_usage { get; set; }
            public int battery_unlocks { get; set; }
        }

        private DateTime[] date = new DateTime[2];
        private MediaElement m_element = new MediaElement();
        private int[] time = new int[2];
        private int[] unlocks = new int[2];
        private int[] total_seconds = new int[2];
        private int diff = 0;

        #region Visibility bools
        private bool _inputBool = false;
        public bool inputBool { get { return _inputBool; } set { Set(ref _inputBool, value); } }
        #endregion

        #region DATA
        private Report _homeData;
        public Report homeData
        {
            get
            {
                return _homeData;
            }
            set
            {
                Set(ref _homeData, value);
            }
        }

        private Records _recordData;
        public Records recordData
        {
            get
            {
                return _recordData;
            }
            set
            {
                Set(ref _recordData, value);
            }
        }

        private List<Hour> _hourList;
        public List<Hour> hourList
        {
            get
            {
                return _hourList;
            }
            set
            {
                Set(ref _hourList, value);
            }
        }

        private bool _editingNote = false;
        public bool editingNote
        {
            get
            {
                return _editingNote;
            }
            set
            {
                Set(ref _editingNote, value);
            }
        }
        #endregion

        #region COMMANDS
        private RelayCommand _RefreshBtn;
        public RelayCommand RefreshBtn
        {
            get
            {
                if(_RefreshBtn == null)
                {
                    _RefreshBtn = new RelayCommand(() => 
                    {
                        Refresh();
                        ShowData();
                    });
                }
                return _RefreshBtn;
            }
        }

        private RelayCommand<object> _ChangeDay;
        public RelayCommand<object> ChangeDay
        {
            get
            {
                if(_ChangeDay == null)
                {
                    _ChangeDay = new RelayCommand<object>((object parameter) =>
                    {
                        int days = int.Parse(parameter.ToString());
                        App.t_client.TrackEvent(days == 1 ? "Next day" : "Previous day");
                        App.report_date = App.report_date.AddDays(days);
                        if (App.report_date.Date == DateTime.Now.Date) { Refresh(); }
                        ShowData();
                    });
                }
                return _ChangeDay;
            }
        }

        private RelayCommand _PickDate;
        public RelayCommand PickDate
        {
            get
            {
                if(_PickDate == null)
                {
                    _PickDate = new RelayCommand(async() => {
                        App.t_client.TrackEvent("Calendar shown");
                        if (await new DateDialog().ShowAsync() == ContentDialogResult.Primary)
                        {
                            Refresh();
                            ShowData();
                        }
                    });
                }
                return _PickDate;
            }
        }

        private RelayCommand _ExportData;
        public RelayCommand ExportData
        {
            get
            {
                if(_ExportData == null)
                {
                    _ExportData = new RelayCommand(() =>
                    {
                        Export();
                    });
                }
                return _ExportData;
            }
        }

        private RelayCommand _EditNote;
        public RelayCommand EditNote
        {
            get
            {
                if(_EditNote == null)
                {
                    _EditNote = new RelayCommand(async() =>
                    {
                        editingNote = !editingNote;
                        App.t_client.TrackEvent(editingNote ? "Edit note" : "Save Note");
                        if (!editingNote)
                        {
                            var day = await Helper.ConnectionDb().Table<Report>().Where(x => x.date == homeData.date).FirstOrDefaultAsync();
                            if(day != null)
                            {
                                if (homeData.note != null && string.IsNullOrWhiteSpace(homeData.note))
                                {
                                    day.note = homeData.note;
                                    await Helper.ConnectionDb().UpdateAsync(day);
                                }
                            }
                            else
                            {
                                await new MessageDialog(utilities.loader.GetString("no_note"), utilities.loader.GetString("error")).ShowAsync();
                                homeData.note = null;
                            }
                        }
                    });
                }
                return _EditNote;
            }
        }
        #endregion

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            Refresh();
            if (App.jump_arguments == "usage")
            {
                Speak();
            }
            App.jump_arguments = null;
            return base.OnNavigatedToAsync(parameter, mode, state); ;
        }

        #region RETRIEVE AND SHOW DATA
        private async void Refresh()
        {
            try
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
                                if (total_seconds[1] <= 9000)
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
                                    await Helper.UpdateTimelineItem(1, time[1], date[1]);
                                }
                            }
                        }
                        else
                        {
                            if (date[0].Date == date[1].Date)
                            {
                                if (unlocks[1] < 5 && diff >= 9000)
                                {
                                }
                                else
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
                                    await Helper.UpdateTimelineItem(unlocks[1], diff, date[1]);
                                }
                            }
                            else if (date[1].DayOfYear - date[0].DayOfYear == 1)
                            {
                                if (total_seconds[1] <= 9000)
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
                        }
                    }
                save:
                    utilities.STATS.Values[settings.date] = date[1].ToString();
                }
            }
            catch (Exception ex)
            {
                App.t_client.TrackException(new ExceptionTelemetry(ex));
                new MessageDialog("A problem occurred when loading data", utilities.loader.GetString("error")).ShowAsync();
            }
        }

        private async void ShowData()
        {
            string date_str = utilities.shortdate_form.Format(App.report_date);
            var data = await Helper.ConnectionDb().Table<Report>().Where(x => x.date == date_str).FirstOrDefaultAsync();
            if(data != null)
            {
                homeData = data;
            }
            else
            {
                homeData = new Report
                {
                    date = date_str,
                    note = null,
                    usage = 0,
                    unlocks = 0
                };
            }
            hourList = await Helper.GetHourList(App.report_date);
        }

        private void ConvertSeconds()
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
            return string.Format(letter, hour, minutes, seconds);
        }

        private async void Speak()
        {
            SpeechSynthesizer syntetizer = new SpeechSynthesizer();
            int[] data = utilities.SplitData(time[1]);
            SpeechSynthesisStream stream = await syntetizer.SynthesizeTextToStreamAsync(String.Format(utilities.loader.GetString("voice_usage"), data[0], data[0] == 1 ? utilities.loader.GetString("hour") : utilities.loader.GetString("hours"), data[1], data[1] == 1 ? utilities.loader.GetString("minute") : utilities.loader.GetString("minutes"), data[2], data[2] == 1 ? utilities.loader.GetString("second") : utilities.loader.GetString("seconds"), unlocks[1], unlocks[1] == 1 ? utilities.loader.GetString("time") : utilities.loader.GetString("times")));
            m_element.SetSource(stream, stream.ContentType);
            m_element.Play();
            MainPage.parameter = null;
        }
        #endregion

        private async void Export()
        {
            try
            {
                var span_result = await new SpanDialog().ShowAsync();
                if (span_result == ContentDialogResult.Primary)
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

        #region UI MANAGEMENT
        private void ManageInputPane()
        {
            InputPane.GetForCurrentView().Showing += (s, args) => { inputBool = true; };
            InputPane.GetForCurrentView().Hiding += (s, args2) => { inputBool = false; };
        }
        #endregion
    }
}
