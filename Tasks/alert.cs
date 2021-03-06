﻿using Database;
using Stuff;
using System;
using System.Collections.Generic;
using Windows.ApplicationModel.Background;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace Tasks
{
    public sealed class alert : IBackgroundTask
    {
        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            BackgroundTaskDeferral _deferral = taskInstance.GetDeferral();
            ToastNotificationActionTriggerDetail detail = taskInstance.TriggerDetails as ToastNotificationActionTriggerDetail;
            int time = int.Parse(detail.UserInput["time"].ToString());
            utilities.STATS.Values[settings.limit] = time * 3600;
            //SCHEDULE A NEW TOAST NOTIFICATION
            string date_str = utilities.shortdate_form.Format(DateTime.Now);
            var query = (await Helper.ConnectionDb().Table<Report>().ToListAsync()).Find(x => x.date == date_str);
            int timex = query == null ? 0 : query.usage;
            int limit = utilities.STATS.Values[settings.limit] == null ? 7200 : int.Parse(utilities.STATS.Values[settings.limit].ToString());
            int span = limit - timex;
            if (span >= 0 && DateTime.Now.AddSeconds(span).Date == DateTime.Now.Date)
            {
                IReadOnlyList<ScheduledToastNotification> list = ToastNotificationManager.CreateToastNotifier().GetScheduledToastNotifications();
                foreach (var toast in list)
                {
                    ToastNotificationManager.CreateToastNotifier().RemoveFromSchedule(toast);
                }
                XmlDocument document = new XmlDocument();
                document.LoadXml(utilities.ToastUsageAlert(limit));
                ScheduledToastNotification scheduled_toast = new ScheduledToastNotification(document, DateTime.Now.AddSeconds(span)) { Tag = utilities.shortdate_form.Format(DateTime.Now) };
                ToastNotificationManager.CreateToastNotifier().AddToSchedule(scheduled_toast);
            }
            _deferral.Complete();
        }
    }
}
