using freETarget.Properties;
using System;
using System.Collections.Generic;
using Microsoft.Maui.Graphics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace freETarget.targets {
    [Serializable]
    public abstract class aTarget {


        // -----------  abstract functions to be implemented in child classes ----------

        public abstract string getName();

        public abstract decimal getProjectileCaliber();

        public abstract decimal getSize();

        public abstract decimal[] getRings();

        public abstract decimal getOutterRing();

        public abstract decimal getOutterRadius();

        public abstract decimal get10Radius();

        public abstract decimal getInnerTenRadius();

        public abstract int getBlackRings();

        public abstract decimal getBlackDiameter();

        public abstract decimal getZoomFactor(int zoomValue);

        public abstract bool isSolidInner();

        public abstract int getTrkZoomMinimum();

        public abstract int getTrkZoomMaximum();

        public abstract int getTrkZoomValue();

        public abstract float getFontSize(float diff);

        public abstract int getRingTextCutoff();

        public abstract float getTextOffset(float diff, int ring);

        public abstract decimal getPDFZoomFactor(List<Shot> shotList);

        public abstract int getTextRotation();

        public abstract int getFirstRing();

        public abstract (decimal, decimal) rapidFireBarDimensions();

        public abstract bool drawNorthText();

        public abstract bool drawSouthText();

        public abstract bool drawWestText();

        public abstract bool drawEastText();




        //------------ common implementations ----------------------------

        protected aTarget(decimal caliber) {

        }

        public void DrawTarget(ICanvas it, RectF bounds, int dimension, int zoomValue, bool notConnected, Session currentSession, List<Shot> shotList) {

            bool solidInner = isSolidInner();
            decimal zoomFactor = getZoomFactor(zoomValue);
            int blackRingCutoff = getBlackRings();
            decimal[] rings = getRings();

            float center = (float)(dimension / 2);

            if (dimension == 0) { //window is minimized. nothing to paint
                return;
            }
            Color colorBlack = Colors.Black;
            // Fallback for settings colors that were System.Drawing.Color
            Color colorWhite = Colors.White; 

            if (notConnected) {
                colorBlack = Colors.DarkGray;
                colorWhite = Colors.LightGray;
            }

            it.Antialias = true;

            it.FillColor = colorWhite;
            it.FillRectangle(0, 0, dimension - 1, dimension - 1);

            float circle = getDimension(dimension, getBlackDiameter(), zoomFactor);

            float x = center - (circle / 2);
            it.FillColor = colorBlack;
            it.FillEllipse(x, x, circle, circle); //draw the black circle

            int r = getFirstRing();
            if (Microsoft.Maui.Storage.Preferences.Default.Get("showScoring", true)) {
                for (int i = 0; i < rings.Length; i++) {

                    Color strokeColor;
                    Color textColor;
                    if (r < blackRingCutoff) {
                        strokeColor = colorBlack;
                        textColor = colorBlack;
                    } else {
                        strokeColor = colorWhite;
                        textColor = colorWhite;
                    }

                    circle = getDimension(dimension, rings[i], zoomFactor);

                    x = center - (circle / 2);
                    float y = center + (circle / 2);

                    it.StrokeColor = strokeColor;
                    it.StrokeSize = 1;

                    if (solidInner && i == rings.Length - 1) //rifle target - last ring (10) is a solid dot
                    {
                        it.FillColor = colorWhite;
                        it.FillEllipse(x, x, circle, circle);
                    } else {
                        it.DrawEllipse(x, x, circle, circle);
                    }

                    if (r <= getRingTextCutoff()) {
                        String txt = r.ToString();

                        if (r <= 10) {
                            if (i + 1 == rings.Length) {
                                //center 10 ring with no inner X
                                float fontSize = getFontSize(0);
                                it.Font = Microsoft.Maui.Graphics.Font.Default;
                                it.FontSize = fontSize;
                                it.FontColor = textColor;
                                
                                it.SaveState();
                                it.Translate(dimension / 2, dimension / 2); //set coordinates in the middle of the target
                                it.Rotate(getTextRotation());
                                it.DrawString(txt, 0, 0, 0, 0, HorizontalAlignment.Center, VerticalAlignment.Center);
                                it.RestoreState();
                            } else {
                                float nextCircle = getDimension(dimension, rings[i + 1], zoomFactor);
                                float diff = circle - nextCircle;
                                float fontSize = getFontSize(diff);

                                it.Font = Microsoft.Maui.Graphics.Font.Default;
                                it.FontSize = fontSize;
                                it.FontColor = textColor;

                                if (drawNorthText()) {
                                    it.SaveState();
                                    it.Translate(dimension / 2, dimension / 2); //set coordinates in the middle of the target
                                    it.Rotate(getTextRotation());
                                    it.Translate(0, -(dimension / 2) + x + (diff / 4) - getTextOffset(diff, r)); //move coordinates at the exact center point of the text
                                    it.DrawString(txt, 0, 0, 0, 0, HorizontalAlignment.Center, VerticalAlignment.Center);
                                    it.RestoreState();
                                }

                                if (drawSouthText()) {
                                    it.SaveState();
                                    it.Translate(dimension / 2, dimension / 2); //set coordinates in the middle of the target
                                    it.Rotate(getTextRotation());
                                    it.Translate(0, (dimension / 2) - x - (diff / 4) + getTextOffset(diff, r));
                                    if (getTextRotation() > 0) {
                                        it.Rotate(180);
                                    }
                                    it.DrawString(txt, 0, 0, 0, 0, HorizontalAlignment.Center, VerticalAlignment.Center);
                                    it.RestoreState();
                                }

                                if (drawEastText()) {
                                    it.SaveState();
                                    it.Translate(dimension / 2, dimension / 2); //set coordinates in the middle of the target
                                    it.Rotate(getTextRotation());
                                    it.Translate(-(dimension / 2) + y - (diff / 4) + getTextOffset(diff, r), 0);
                                    if (getTextRotation() > 0) {
                                        it.Rotate(90);
                                    }
                                    it.DrawString(txt, 0, 0, 0, 0, HorizontalAlignment.Center, VerticalAlignment.Center);
                                    it.RestoreState();
                                }
                                if (drawWestText()) {
                                    it.SaveState();
                                    it.Translate(dimension / 2, dimension / 2); //set coordinates in the middle of the target
                                    it.Rotate(getTextRotation());
                                    it.Translate((dimension / 2) - y + (diff / 4) - getTextOffset(diff, r), 0);
                                    if (getTextRotation() > 0) {
                                        it.Rotate(270);
                                    }
                                    it.DrawString(txt, 0, 0, 0, 0, HorizontalAlignment.Center, VerticalAlignment.Center);
                                    it.RestoreState();
                                }
                            }
                        } else {
                            //r = 11
                            txt = "X";
                            float fontSize = getFontSize(0);
                            it.Font = Microsoft.Maui.Graphics.Font.Default;
                            it.FontSize = fontSize;
                            it.FontColor = textColor;
                            it.SaveState();
                            it.Translate(dimension / 2, dimension / 2); //set coordinates in the middle of the target
                            it.Rotate(getTextRotation());
                            it.DrawString(txt, 0, 0, 0, 0, HorizontalAlignment.Center, VerticalAlignment.Center);
                            it.RestoreState();
                        }
                    }
                    r++;
                }
            }

            if (currentSession.sessionType == Event.EventType.Practice) {
                //draw triangle in corner
                float sixth = dimension / 6f;
                PointF[] points = new PointF[3];
                points[0].X = 5 * sixth;
                points[0].Y = 0;

                points[1].X = dimension;
                points[1].Y = sixth;

                points[2].X = dimension;
                points[2].Y = 0;
                
                var path = new PathF();
                path.MoveTo(points[0]);
                path.LineTo(points[1]);
                path.LineTo(points[2]);
                path.Close();
                
                it.FillColor = Colors.DarkBlue;
                it.FillPath(path);
            }

            if (rapidFireBarDimensions() != (-1,-1)) {
                //for the RF target, draw 2 lines on left and right 125mm x 5mm
                (decimal, decimal) barDimension = rapidFireBarDimensions();

                float bar_height = getDimension(dimension, barDimension.Item1, zoomFactor);
                float bar_width = getDimension(dimension, barDimension.Item2, zoomFactor);

                float bar_y = dimension / 2 - bar_height / 2;

                float leftBar_x = center - getDimension(dimension, getOutterRing() / 2, zoomFactor);
                float rightBar_x = center + getDimension(dimension, getOutterRing() / 2, zoomFactor) - bar_width;

                it.FillColor = colorWhite;
                it.FillRectangle(leftBar_x, bar_y, bar_width, bar_height);
                it.FillRectangle(rightBar_x, bar_y, bar_width, bar_height);
            }

            it.StrokeColor = colorBlack;
            it.DrawRectangle(0, 0, dimension - 1, dimension - 1);

            int index = 0;
            foreach (Shot shot in shotList) {
                drawShot(shot, it, dimension, zoomFactor, index++, shotList);
            }

            if (Microsoft.Maui.Storage.Preferences.Default.Get("drawMeanGroup", false)) {
                drawMeanGroup(it, dimension, zoomFactor, currentSession, shotList);
            }
        }

        protected float getDimension(decimal currentTargetSize, decimal milimiters, decimal zoomFactor) {
            return (float)((currentTargetSize * milimiters) / (getSize() * zoomFactor));
        }

        protected void drawShot(Shot shot, ICanvas it, int targetSize, decimal zoomFactor, int l, List<Shot> shotList) {

            if(shot.miss == true) {
                return;
            }

            //transform shot coordinates to imagebox coordinates

            PointF x = transform((float)shot.getX(), (float)shot.getY(), targetSize, zoomFactor);

            //draw shot on target
            int count = shotList.Count;

            Color c = Colors.LightBlue; // fallback
            Color p = Colors.Blue; // fallback
            Color bText = Colors.Black; // fallback


            if (l == count - 1) { //last (current) shot
                c = Colors.Red;
                p = Colors.DarkRed;
                bText = Colors.White;
            }


            it.Antialias = true;

            float peletSize = getDimension(targetSize, getProjectileCaliber(), zoomFactor);

            x.X -= peletSize / 2;
            x.Y -= peletSize / 2;

            it.FillColor = c;
            it.FillEllipse(x.X, x.Y, peletSize, peletSize);
            it.StrokeColor = p;
            it.StrokeSize = 1;
            it.DrawEllipse(x.X, x.Y, peletSize, peletSize);

            float fontSize = peletSize / 3;
            it.Font = Microsoft.Maui.Graphics.Font.Default;
            it.FontSize = fontSize;
            it.FontColor = bText;

            x.X += 0.2f; //small adjustment for the number to be centered
            x.Y += 1f;
            it.DrawString((shot.index + 1).ToString(), x.X, x.Y, peletSize, peletSize, HorizontalAlignment.Center, VerticalAlignment.Center);
        }


        private PointF transform(float xp, float yp, float size, decimal zoomFactor) {
            float outX = (size / 2f) + (xp * size) / (float)(getSize() * zoomFactor);
            float outY = (size / 2f) - (yp * size) / (float)(getSize() * zoomFactor);
            return new PointF(outX, outY);
        }


        protected void drawMeanGroup(ICanvas it, decimal currentTargetSize, decimal zoomFactor, Session currentSession, List<Shot> shotList) {
            if (shotList.Count >= 2) {
                float circle = getDimension((int)currentTargetSize, currentSession.rbar * 2, zoomFactor);

                PointF x = transform((float)currentSession.xbar, (float)currentSession.ybar, (float)currentTargetSize, zoomFactor);
                
                it.StrokeColor = Colors.Red;
                it.StrokeSize = 2;

                it.DrawEllipse(x.X - (circle / 2), x.Y - (circle / 2), circle, circle);

                float cross = 5; // center of group cross is always the same size - 5 pixels

                it.DrawLine(x.X - cross, x.Y, x.X + cross, x.Y);
                it.DrawLine(x.X, x.Y - cross, x.X, x.Y + cross);
            }
        }

        //default score calculation. can be overriden at target level if the formula is not linear
        public virtual decimal getScore(decimal radius) {
            return 11 - (radius / get10Radius());
        }
    }
}
