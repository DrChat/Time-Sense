﻿namespace Time_Sense.ViewModels
{
    public class ViewModelLocator
    {
        public ViewModelLocator()
        {
        }

        public HomeViewModel Home
        {
            get
            {
                return new HomeViewModel();
            }
        }

        public TimelineViewModel Timeline
        {
            get
            {
                return new TimelineViewModel();
            }
        }

        public ReportViewModel Report
        {
            get
            {
                return new ReportViewModel();
            }
        }

        public AnalysisViewModel Analysis
        {
            get
            {
                if(App.analysis_page == null)
                {
                    App.analysis_page = new AnalysisViewModel();
                }
                return App.analysis_page;
            }
        }
    }
}
