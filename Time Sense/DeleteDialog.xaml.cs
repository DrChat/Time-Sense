﻿using System;
using System.Collections.Generic;
using Database;
using Stuff;
using Windows.UI.Popups;
using Windows.UI.Xaml.Controls;

namespace Time_Sense
{
    public sealed partial class DeleteDialog : ContentDialog
    {

        public DeleteDialog(int mode)
        {
            this.InitializeComponent();
            Start(mode);
        }

        private async void Start(int mode)
        {
            App.dialog = true;
            if (mode == 0)
            {
                try
                {
                    await Database.Helper.InitializeDatabase();
                    int step = 0;
                    var report_query = await Database.Helper.ConnectionDb().Table<Database.Report>().ToListAsync();
                    var hour_query = await Database.Helper.ConnectionDb().Table<Database.Hour>().ToListAsync();
                    var timeline_query = await Database.Helper.ConnectionDb().Table<Database.Timeline>().ToListAsync();
                    int max = report_query.Count + hour_query.Count + timeline_query.Count;
                    bar.Maximum = max;
                    foreach (var item in report_query)
                    {
                        if (item != null)
                        {
                            await Database.Helper.ConnectionDb().DeleteAsync(item);
                            step++;
                            bar.Value = step;
                            delete_progress.Text = String.Format(utilities.loader.GetString("delete_dialog_progress"), step, max);
                        }
                    }
                    foreach (var item in hour_query)
                    {
                        if (item != null)
                        {
                            await Database.Helper.ConnectionDb().DeleteAsync(item);
                            step++;
                            bar.Value = step;
                            delete_progress.Text = String.Format(utilities.loader.GetString("delete_dialog_progress"), step, max);
                        }
                    }
                    foreach (var item in timeline_query)
                    {
                        if (item != null)
                        {
                            await Database.Helper.ConnectionDb().DeleteAsync(item);
                            step++;
                            bar.Value = step;
                            delete_progress.Text = String.Format(utilities.loader.GetString("delete_dialog_progress"), step, max);
                        }
                    }
                    MessageDialog success_error_message = new MessageDialog(utilities.loader.GetString("delete_dialog_success"), utilities.loader.GetString("success"));
                    await success_error_message.ShowAsync();
                    this.Hide();
                }
                catch
                {
                    MessageDialog delete_error_message = new MessageDialog(utilities.loader.GetString("delete_dialog_error"), utilities.loader.GetString("error"));
                    await delete_error_message.ShowAsync();
                    this.Hide();
                }
            }
            else
            {
                try
                {
                    //DELETE RANGE
                    int days = SettingsPage.delete_date[1].Date.Subtract(SettingsPage.delete_date[0].Date).Days;
                    int step = 0;
                    var t = new List<Timeline>();
                    var r = new List<Report>();
                    var h = new List<Hour>();
                    for (int i = 0; i <= days; i++)
                    {
                        string date_str = utilities.shortdate_form.Format(SettingsPage.delete_date[0].Date.AddDays(i));
                        Report d_item = await Helper.ConnectionDb().Table<Report>().Where(x => x.date == date_str).FirstOrDefaultAsync();
                        var t_list = await Helper.ConnectionDb().Table<Timeline>().Where(x => x.date == date_str).ToListAsync();
                        var h_list = await Helper.ConnectionDb().Table<Hour>().Where(x => x.date == date_str).ToListAsync();
                        if (d_item != null)
                        {
                            r.Add(d_item);
                        }
                        foreach (var item in t_list)
                        {
                            if (item != null)
                            {
                                t.Add(item);
                            }
                        }
                        foreach (var item in h_list)
                        {
                            if (item != null)
                            {
                                h.Add(item);
                            }
                        }
                    }
                    int max = r.Count + t.Count + h.Count;
                    bar.Maximum = max;
                    foreach (var item in r)
                    {
                        if (item != null)
                        {
                            await Helper.ConnectionDb().DeleteAsync(item);
                            step++;
                            bar.Value = step;
                            delete_progress.Text = String.Format(utilities.loader.GetString("delete_dialog_progress"), step, max);
                        }
                    }
                    foreach (var item in t)
                    {
                        if (item != null)
                        {
                            await Helper.ConnectionDb().DeleteAsync(item);
                            step++;
                            bar.Value = step;
                            delete_progress.Text = String.Format(utilities.loader.GetString("delete_dialog_progress"), step, max);
                        }
                    }
                    foreach (var item in h)
                    {
                        if (item != null)
                        {
                            await Helper.ConnectionDb().DeleteAsync(item);
                            step++;
                            bar.Value = step;
                            delete_progress.Text = String.Format(utilities.loader.GetString("delete_dialog_progress"), step, max);
                        }
                    }
                    MessageDialog success_error_message = new MessageDialog(utilities.loader.GetString("delete_dialog_success"), utilities.loader.GetString("success"));
                    await success_error_message.ShowAsync();
                    this.Hide();
                }
                catch
                {
                    MessageDialog delete_error_message = new MessageDialog(utilities.loader.GetString("delete_dialog_error"), utilities.loader.GetString("error"));
                    await delete_error_message.ShowAsync();
                    this.Hide();
                }

            }

        }
    }
}
