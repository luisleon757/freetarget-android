using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using Microcharts;
using SkiaSharp;
using freETargetMAUI.Graphics;
using freETargetMAUI.Services;
using freETargetMAUI.Models;
using freETarget;
using freETarget.targets;
using Microsoft.Maui.Graphics;

namespace freETargetMAUI
{
    public partial class AnalyticsPage : ContentPage
    {
        private StorageController _storageController;
        private TargetDrawable _heatmapDrawable;

        public AnalyticsPage()
        {
            InitializeComponent();
            _heatmapDrawable = new TargetDrawable();
            HeatmapGraphicsView.Drawable = _heatmapDrawable;
        }

        public void InitializeWithStorage(StorageController storageController, string currentShooterName)
        {
            _storageController = storageController;
            LoadShooters(currentShooterName);
        }

        private void LoadShooters(string defaultName)
        {
            var users = _storageController.findAllUsers();
            ShooterPicker.ItemsSource = users;
            if (users.Contains(defaultName))
            {
                ShooterPicker.SelectedItem = defaultName;
            }
            else if (users.Count > 0)
            {
                ShooterPicker.SelectedIndex = 0;
            }
        }

        private void OnShooterSelected(object sender, EventArgs e)
        {
            if (ShooterPicker.SelectedIndex != -1)
            {
                string shooter = (string)ShooterPicker.SelectedItem;
                GenerateAnalyticsForShooter(shooter);
            }
        }

        private void GenerateAnalyticsForShooter(string userName)
        {
            var summaries = _storageController.findAllSessionSummariesForUser(userName);
            
            // Filter match sessions or valid ones
            var validSessions = summaries.Where(s => s.Score > 0).OrderBy(s => s.Id).ToList();

            if (validSessions.Count == 0)
            {
                MediaScoreLabel.Text = "-";
                BestScoreLabel.Text = "-";
                AvgGroupLabel.Text = "-";
                EvolutionChart.Chart = new LineChart();
                return;
            }

            // 1. Calcular KPIs
            double averageScore = validSessions.Average(s => (double)s.DecimalScore);
            decimal bestScore = validSessions.Max(s => s.DecimalScore);
            
            MediaScoreLabel.Text = averageScore.ToString("F1");
            BestScoreLabel.Text = bestScore.ToString("F1");

            // 2. Gráfico de Evolución
            var chartEntries = new List<ChartEntry>();
            foreach (var s in validSessions.Skip(Math.Max(0, validSessions.Count - 20))) // Últimas 20 sesiones
            {
                chartEntries.Add(new ChartEntry((float)s.DecimalScore)
                {
                    Label = s.Id.ToString(),
                    ValueLabel = s.DecimalScore.ToString("F1"),
                    Color = SKColor.Parse("#3B82F6")
                });
            }

            EvolutionChart.Chart = new LineChart
            {
                Entries = chartEntries,
                LineMode = LineMode.Straight,
                LineSize = 4,
                PointMode = PointMode.Circle,
                PointSize = 10,
                BackgroundColor = SKColors.Transparent,
                LabelTextSize = 25f,
                ValueLabelOrientation = Orientation.Horizontal,
                LabelOrientation = Orientation.Horizontal,
                Margin = 20
            };

            // 3. Generar Heatmap
            AggregateShotsForHeatmap(validSessions);
        }

        private void AggregateShotsForHeatmap(List<SessionSummary> validSessions)
        {
            // Extraer completo de las últimas 5 sesiones
            var recentSessionIds = validSessions.OrderByDescending(s => s.Id).Take(5).Select(s => s.Id).ToList();
            
            List<Shot> aggregatedShots = new List<Shot>();
            Event sampleEventType = null;
            Session dummySession = null;

            double sumGroupSize = 0;
            int countGroups = 0;

            foreach(var id in recentSessionIds)
            {
                var fullSesh = _storageController.findSession(id);
                if (fullSesh != null && fullSesh.Shots != null)
                {
                    aggregatedShots.AddRange(fullSesh.Shots);
                    if (dummySession == null) {
                        dummySession = fullSesh;
                        sampleEventType = fullSesh.eventType;
                    }
                    if (fullSesh.groupSize > 0)
                    {
                        sumGroupSize += (double)fullSesh.groupSize;
                        countGroups++;
                    }
                }
            }

            if (countGroups > 0) {
                AvgGroupLabel.Text = (sumGroupSize / countGroups).ToString("F1") + "mm";
            } else {
                AvgGroupLabel.Text = "-";
            }

            if (dummySession == null) return;

            // Resetting properties for transparent overlapping
            _heatmapDrawable.Target = sampleEventType.Target;
            _heatmapDrawable.CurrentSession = dummySession;
            _heatmapDrawable.Shots = aggregatedShots;
            
            HeatmapGraphicsView.Invalidate();
        }

        private async void OnCloseClicked(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }
    }
}
