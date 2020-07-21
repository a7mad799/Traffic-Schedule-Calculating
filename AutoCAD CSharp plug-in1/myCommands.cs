// (C) Copyright 2020 by Ahmad Asela
//
using System;
using System.Diagnostics;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using System.Collections.Generic;
using Decider;
using System.Linq;
using Decider.Csp.Integer;
using Decider.Csp.Global;
using Decider.Csp.BaseTypes;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.Windows;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections;


// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(AutoCAD_CSharp_plug_in1.MyCommands))]

namespace AutoCAD_CSharp_plug_in1
{
    // This class is instantiated by AutoCAD for each document when
    // a command is called by the user the first time in the context
    // of a given document. In other words, non static data in this class
    // is implicitly per-document!
    public class MyCommands
    {
        // The CommandMethod attribute can be applied to any public  member 
        // function of any public class.
        // The function should take no arguments and return nothing.
        // If the method is an intance member then the enclosing class is 
        // intantiated for each document. If the member is a static member then
        // the enclosing class is NOT intantiated.
        //
        // NOTE: CommandMethod has overloads where you can provide helpid and
        // context menu.

        // Modal Command with localized name
        [CommandMethod("MyGroup", "CalculateLoad", "CalculateLoadLocal", CommandFlags.Modal)]
        public void CalculateLoad() // This method can have any name
        {
            // Put your command code here
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed;
            if (doc != null)
            {
                ed = doc.Editor;
                OpenFileDialog ofd =
                new OpenFileDialog("Select data file to calculate schedule", null, "xml", "DataFileToCalculate",
                                     OpenFileDialog.OpenFileDialogFlags.DoNotTransferRemoteFiles);

                System.Windows.Forms.DialogResult dr = ofd.ShowDialog();
                if (dr != System.Windows.Forms.DialogResult.OK)
                    return;

                string[] lines = File.ReadAllLines(ofd.Filename);

                bool routesReading = false;
                bool pedesRoutesReading = false;
                string pattern = @"\d+(\.\d+)?,\d+(\.\d+)?(,\d+(\.\d+)?)?";
                Regex rgx = new Regex(pattern);
                MatchCollection matches;
                ArrayList pedesConnectors = new ArrayList();
                List<Curve> connectors = new List<Curve>();
                List<int> loadForConnectors = new List<int>();
                List<int> timeForPedes = new List<int>();
                List<Point3dCollection> curvesPointsColl = new List<Point3dCollection>();


                foreach (string line in lines)
                {
                    if (line == "<routes>")
                        routesReading = true;
                    else if (line == "</routes>")
                        routesReading = false;
                    else if (line == "<pedestrain_routes>")
                        pedesRoutesReading = true;
                    else if (line == "</pedestrain_routes>")
                        pedesRoutesReading = false;

                    else if (routesReading)
                    {
                        matches = rgx.Matches(line);
                        if (matches.Count > 0)
                        {
                            if (matches.Count == 3)
                            {
                                string[] coords = matches[0].Value.Split(',');

                                Point3d point1 = new Point3d(double.Parse(coords[0]), double.Parse(coords[1]), 0);
                                coords = matches[1].Value.Split(',');
                                Point3d point2 = new Point3d(double.Parse(coords[0]), double.Parse(coords[1]), 0);
                                coords = matches[2].Value.Split(',');

                                Line connector = new Line(point1, point2);
                                loadForConnectors.Add((int)double.Parse(coords[0])); 
                                connector.ColorIndex = (int)double.Parse(coords[1]) + 3;
                                connectors.Add(connector);
                            }

                            else if (matches.Count == 4)
                            {
                                Point3dCollection pntSet = new Point3dCollection();
                                string[] coords = matches[0].Value.Split(',');
                                pntSet.Add(new Point3d(double.Parse(coords[0]), double.Parse(coords[1]), 0));
                                coords = matches[1].Value.Split(',');
                                pntSet.Add(new Point3d(double.Parse(coords[0]), double.Parse(coords[1]), 0));
                                coords = matches[2].Value.Split(',');
                                pntSet.Add(new Point3d(double.Parse(coords[0]), double.Parse(coords[1]), 0));
                                coords = matches[3].Value.Split(',');

                                //PolylineCurve3d leftTurn = new PolylineCurve3d(pntSet);
                                Polyline3d turn = new Polyline3d(Poly3dType.CubicSplinePoly, pntSet, false);
                                loadForConnectors.Add(int.Parse(coords[0]));
                                turn.ColorIndex = int.Parse(coords[1]) + 3;
                                connectors.Add(turn);
                                curvesPointsColl.Add(pntSet);
                            }
                            
                            }
                        }
                    else if (pedesRoutesReading)
                    {
                        matches = rgx.Matches(line);
                        if (matches.Count > 0)
                        {
                            if (matches.Count == 3)
                            {
                                string[] coords = matches[0].Value.Split(',');

                                Point3d point1 = new Point3d(double.Parse(coords[0]), double.Parse(coords[1]), 0);
                                coords = matches[1].Value.Split(',');
                                Point3d point2 = new Point3d(double.Parse(coords[0]), double.Parse(coords[1]), 0);
                                coords = matches[2].Value.Split(',');

                                Line connector = new Line(point1, point2);
                                timeForPedes.Add(int.Parse(coords[0]));
                                connector.ColorIndex = int.Parse(coords[1]) + 3;
                                List<Line> collection = new List<Line>();
                                collection.Add(connector);
                                pedesConnectors.Add(collection);
                            }



                            else
                            {
                                ed.WriteMessage("the data file is written wrongly\n");
                                return;
                            }
                        }
                    }
                }

                Transaction tr = db.TransactionManager.StartTransaction();

                string routesToWrite = "<routes>\n";
                string pedesRoutesToWrite = "<pedestrain_routes>\n";

                List<VariableInteger> cases = new List<VariableInteger>();
                using (tr)
                {
                    BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    foreach (Curve connector in connectors)
                    {
                        btr.AppendEntity(connector); // drawing the straight routes
                        tr.AddNewlyCreatedDBObject(connector, true);
                    }
                    foreach(List<Line> pedesCol in pedesConnectors)
                    {
                        btr.AppendEntity(pedesCol[0]); // drawing the straight routes
                        tr.AddNewlyCreatedDBObject(pedesCol[0], true);
                    }
                    //ed.WriteMessage("\nFile selected was \"{0}\".", ofd.Filename);

                    var constraints = new List<IConstraint>();
                    for (int i1 = 0; i1 < connectors.Count + pedesConnectors.Count; i1++)
                    {
                        cases.Add(new VariableInteger("" + i1, 0, 10));
                    }

                    for (int i1 = 0; i1 < connectors.Count; i1++)
                    {
                        for (int j1 = i1 + 1; j1 < connectors.Count; j1++)
                        {
                            //ed.WriteMessage("\t" + connectors[i1].GetGeCurve().GetDistanceTo(connectors[j1].GetGeCurve()));

                            if (connectors[i1].StartPoint != connectors[j1].StartPoint
                                && connectors[i1].GetGeCurve().GetDistanceTo(connectors[j1].GetGeCurve()) < 3)
                            {
                                //return;
                                constraints.Add(new ConstraintInteger(cases[i1] != cases[j1]));
                            }
                            else if (connectors[i1].StartPoint == connectors[j1].StartPoint)
                            {
                                constraints.Add(new ConstraintInteger(cases[i1] == cases[j1]));
                            }

                        }

                        for (int i2 = 0; i2 < pedesConnectors.Count; i2++)
                        {
                            List<Line> temp = (List<Line>)(pedesConnectors[i2]);
                            Line connector = temp[0];
                            if (connectors[i1].GetGeCurve().GetDistanceTo(connector.GetGeCurve()) < 3)
                                constraints.Add(new ConstraintInteger(cases[i1] != cases[i2 + connectors.Count]));
                        }
                    }


                    IState<int> state = new StateInteger(cases, constraints);
                    state.StartSearch(out StateOperationResult searchResult);

                    ed.WriteMessage("\nhello");

                    db.TransactionManager.QueueForGraphicsFlush();


                    List<int> distinctCases = new List<int>();
                    foreach (VariableInteger _case in cases)
                    {
                        distinctCases.Add(_case.Value);
                    }
                    distinctCases = distinctCases.Distinct().ToList();
                    distinctCases.Sort();



                    for (int i1 = 0; i1 < connectors.Count; i1++)
                    {

                        connectors[i1].ColorIndex = 3 + cases[i1].Value;
                    }

                    for (int i1 = 0; i1 < pedesConnectors.Count; i1++)
                    {
                        List<Line> temp = (List<Line>)(pedesConnectors[i1]);

                        temp[0].ColorIndex = 3 + cases[i1 + connectors.Count].Value;
                    }
                    foreach (List<Line> pedesCol in pedesConnectors)
                    {
                        Line pedes = pedesCol[0];
                        List<bool> availableCases = new List<bool>();
                        for (int i1 = 0; i1 < distinctCases.Count; i1++)
                        {
                            availableCases.Add(true);
                        }

                        bool free = true;
                        for (int i2 = 0; i2 < connectors.Count; i2++)
                        {
                            Curve connector = connectors[i2];
                            if (pedes.GetGeCurve().GetDistanceTo(connector.GetGeCurve()) < 3)
                            {
                                availableCases[connector.ColorIndex - 3] = false;
                                free = false;
                            }
                        }
                        Line current = pedes;
                        for (int i2 = 0; i2 < availableCases.Count; i2++)
                            if (availableCases[i2] && pedes.ColorIndex != 3 + i2 && !free)
                            {
                                Point3d additionStart = new Point3d(pedes.StartPoint.X + i2 * Math.Cos(pedes.Angle),
                                                                pedes.StartPoint.Y + i2 * Math.Sin(pedes.Angle), 0);
                                Point3d additionEnd = new Point3d(pedes.EndPoint.X + i2 * Math.Cos(pedes.Angle),
                                                                    pedes.EndPoint.Y + i2 * Math.Sin(pedes.Angle), 0);
                                DBObjectCollection acDbObjColl = current.GetOffsetCurves(0.5);
                                Line available = (Line)acDbObjColl[0];
                                current = available;
                                available.ColorIndex = 3 + i2;
                                pedesCol.Add(available);
                                btr.AppendEntity(available); // drawing the straight routes
                                tr.AddNewlyCreatedDBObject(available, true);
                            }
                    }

                    db.TransactionManager.QueueForGraphicsFlush();
                    List<int> loads = new List<int>();

                        for (int i1 = 0; i1 < distinctCases.Count; i1++)
                        {
                            int totalLoad = 0;
                            for (int i = 0; i < connectors.Count; i++)
                            {
                                Curve connector = (Curve)connectors[i];
                                if (connector.ColorIndex == i1 + 3)
                                    totalLoad += loadForConnectors[i];
                            }
                            loads.Add(totalLoad);
                        }


                        ed.WriteMessage("We found " + distinctCases.Count + " phases:-\n");
                        int caseCapacity;
                        List<double> caseLengthAvg = new List<double>();
                        List<int> caseGreenTime = new List<int>(); // seconds

                        foreach (int _case in distinctCases)
                        {// loadAvg * lengthAvg / 10

                            caseCapacity = 0;
                            caseLengthAvg.Add(0);
                            foreach (Curve connector in connectors)
                            {
                                if (connector.ColorIndex == 3 + _case)
                                {
                                    caseCapacity++;
                                    caseLengthAvg[_case] += connector.GetDistanceAtParameter(connector.EndParam - connector.StartParam);
                                //calculating the average total for each case 
                            }

                        }
                            caseLengthAvg[_case] /= caseCapacity;
                            loads[_case] /= caseCapacity;
                            caseGreenTime.Add((int)(caseLengthAvg[_case] * loads[_case] / 40) + 5);

                        }

                        ed.WriteMessage("\nPlease enter the needed time ");
                        for (int i = 0; i < pedesConnectors.Count; i++)
                        {
                            List<Line> pedesCol1 = (List<Line>)pedesConnectors[i];
                            int t = timeForPedes[i];
                            int totalTime = 0;
                            foreach (Line pedes1 in pedesCol1)
                            {
                                totalTime += caseGreenTime[pedes1.ColorIndex - 3];
                            }
                            if (t > totalTime)
                            {
                                foreach (Line pedes1 in pedesCol1)
                                {
                                    caseGreenTime[pedes1.ColorIndex - 3] += (t - totalTime) / pedesCol1.Count;
                                }
                            }
                            //var a = Color.FromColorIndex(ColorMethod.ByAci, (short)(_case.Value + 3)).ColorName;
                        }
                        if (caseGreenTime.Count > 0)
                        {
                            double ratio = (double)120 / (caseGreenTime.Sum() - caseGreenTime.Min()); // in order to resrict waiting time from being more than 120
                            if (ratio < 1)
                            {
                                foreach (int _case in distinctCases)
                                {
                                    caseGreenTime[_case] = (int)(caseGreenTime[_case] * ratio);
                                }
                            }
                        }
                        int j2 = 0;
                        foreach (Curve connector in connectors)
                        {
                            var connectorType = connector.GetType();
                            if (connectorType.Name == "Line")
                            {
                                routesToWrite = routesToWrite + "\t<route>" + connector.StartPoint + ", " + connector.EndPoint + ", " +
                                    loads[connector.ColorIndex - 3] + "," + (connector.ColorIndex - 3) + " </route>\n";
                            }
                            else
                            {
                                routesToWrite = routesToWrite + "\t<route>" + curvesPointsColl[j2][0] + ", " + curvesPointsColl[j2][1] + ", "
                                    + curvesPointsColl[j2][2] + ", " + loads[connector.ColorIndex - 3] + "," + (connector.ColorIndex - 3) + " </route>\n";
                                j2++;
                            }
                        }
                        routesToWrite = routesToWrite + "</routes>\n";

                        foreach (List<Line> pedesCol1 in pedesConnectors)
                        {
                            pedesRoutesToWrite = pedesRoutesToWrite + "\t<route>" + pedesCol1[0].StartPoint + ", " + pedesCol1[0].EndPoint + ", " +
                                    caseGreenTime[pedesCol1[0].ColorIndex - 3] + "," + (pedesCol1[0].ColorIndex - 3) + " </route>\n";
                        }

                        pedesRoutesToWrite = pedesRoutesToWrite + "</pedestrain_routes>\n";

                        foreach (int _case in distinctCases)
                        {
                            ed.WriteMessage("for the " + Color.FromColorIndex(ColorMethod.ByColor, (short)(_case + 3)).ColorNameForDisplay +
                                                                    " routes we need: " + caseGreenTime[_case] + " seconds\n");
                        }

                        PromptKeywordOptions KeyOptions = new PromptKeywordOptions("")
                        {
                            Message = "Would you like to export the data? \n",
                            AllowNone = false
                        };
                        KeyOptions.Keywords.Add("Yes");
                        KeyOptions.Keywords.Add("No");
                        PromptResult KeyRes = ed.GetKeywords(KeyOptions);
                        if (KeyRes.StringResult == "Yes")
                        {
                            SaveFileDialog sfd = new SaveFileDialog("Save schedule calculating data file results", null, "xml", "DataFileToCalculate",
                                            SaveFileDialog.SaveFileDialogFlags.DoNotTransferRemoteFiles);

                            if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                            {
                                File.WriteAllText(sfd.Filename, routesToWrite + "\n" + pedesRoutesToWrite);
                            }

                        }
                    
                    tr.Commit();
                }

                
            }
        }


        // Modal Command with pickfirst selection
        [CommandMethod("MyGroup", "CalculateSchedule", "MyPickFirstLocal", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void CalculateSchedule() // This method can have any name
        {
           // var s = new Decider.Csp.Integer.VariableInteger();
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            PromptSelectionResult psr = doc.Editor.GetSelection();
            if (psr.Status == PromptStatus.OK)
            {
                if (doc != null)
                { 
                   // ed.WriteMessage("We found 3 phases:-\n\tFrom right to left and left to right together: 1 minute" +
                   //                                    "\n\tFrom right to down: 20 second" +
                   //                                    "\n\tFrom down to left: 25");
                }
                List<Curve> connectors = new List<Curve>();
                List<Point3dCollection> curvesPointsColl = new List<Point3dCollection>();

                Transaction tr = db.TransactionManager.StartTransaction();
                ArrayList pedesConnectors = new ArrayList();
                string routesToWrite = "<routes>\n";
                string pedesRoutesToWrite = "<pedestrain_routes>\n";

                List<VariableInteger> cases = new List<VariableInteger>();
                using (tr)
                {
                    bool firstline = true;
                    Line prevLine = null;
                    double distance = 0;
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                    RXClass lineClass = RXClass.GetClass(typeof(Line));
                    List<Line> myLines = new List<Line>();
                    List<Shape> shapes = new List<Shape>();

                    foreach (SelectedObject so in psr.Value)
                    {

                        if (so.ObjectId.ObjectClass.IsDerivedFrom(lineClass))
                        {
                            //this variable reads the selected objects then we choose the lines only for analyzing
                            Line line = (Line)tr.GetObject(so.ObjectId, OpenMode.ForRead);
                            line = (Line)tr.GetObject(so.ObjectId, OpenMode.ForWrite);
                            myLines.Add(line);


                            if (!firstline) {
                                distance = line.StartPoint.X - prevLine.StartPoint.X;
                            }
                            //ed.WriteMessage("\nStartPoint: {0} \nEndPoint: {1}\nsteps: {2}", line.StartPoint, line.EndPoint, distance);
                            prevLine = line;
                            firstline = false;
                        }

                    }

                    Line curr = new Line();
                    Line line1 = new Line();
                    Line line2 = new Line();
                    int i, foundLines, checkCount, nullCount = 0;
                    for (int l = 0; l < myLines.Count; l++)
                    {
                        if (myLines[l] != null)
                        {
                            Shape newShape = new Shape();
                            newShape.lines.Add(myLines[l]);
                            line1 = myLines[l];
                            curr.EndPoint = line1.EndPoint;
                            curr.StartPoint = line1.StartPoint;
                            myLines[l] = null;
                            nullCount++;
                            checkCount = 0;
                            i = 0;
                            foundLines = 1;
                            while (curr.EndPoint != curr.StartPoint && nullCount < myLines.Count)// checking adjacents
                            {
                                checkCount++;
                                if (myLines[i] != null)
                                {
                                    line2 = myLines[i];
                                    if ((curr.EndPoint - line2.StartPoint).Length < 0.001 && line1 != line2)
                                    {
                                        checkCount = 0;
                                        foundLines++;
                                        curr.EndPoint = line2.EndPoint;
                                        //ed.WriteMessage(foundLines + "\t");
                                        newShape.lines.Add(myLines[i]);
                                        myLines[i] = null;
                                        nullCount++;

                                    }
                                    else if ((curr.EndPoint - line2.EndPoint).Length < 0.001 && line1 != line2)
                                    {
                                        checkCount = 0;
                                        foundLines++;
                                        curr.EndPoint = line2.StartPoint;
                                        //ed.WriteMessage(foundLines + "\t");
                                        myLines[i].ReverseCurve();
                                        newShape.lines.Add(myLines[i]);
                                        myLines[i] = null;
                                        nullCount++;

                                    }
                                    if (foundLines == 100)
                                    {
                                        newShape = null;
                                        //ed.WriteMessage("\ninfinite loop");
                                        break;
                                    }
                                }
                                if (checkCount > 2 * myLines.Count)
                                {
                                    newShape = null;
                                    checkCount = 0;
                                    //ed.WriteMessage("\ninfinite checks");
                                    break;
                                }



                                //break;  
                                i = (i + 1) % myLines.Count;
                            }
                            if (newShape != null && (curr.EndPoint - curr.StartPoint).Length < 0.001)
                            {
                                // new shapae has been analyzed 
                                shapes.Add(newShape);
                            }
                        }


                    }
                    //Point3d temp;
                    foreach (Shape item in shapes)
                    {
                        if (item.lines.Count == 4 && item.lines[0].Length - item.lines[2].Length < 0.0001
                                                  && item.lines[1].Length - item.lines[3].Length < 0.0001)
                        {
                            item.Type = "pedestrian";
                        }
                        else if (item.lines.Count > 6)
                        {
                            double angle, len1, len2;
                            Line virtu = new Line();

                            for (int l = 0; l < item.lines.Count; l++)
                            {

                                if (Math.Abs(item.lines[l].Angle - (item.lines[(l + 2) % item.lines.Count].Angle + Math.PI) % (Math.PI * 2)) < 0.01 &&
                                    Math.Abs(item.lines[l].Angle % Math.PI - (item.lines[(l + 1) % item.lines.Count].Angle + (Math.PI / 2)) % Math.PI) < 0.01)
                                {
                                    item.startAngle = item.lines[(l + 2) % item.lines.Count].Angle;
                                    //ed.WriteMessage("\nstartAngle" + item.startAngle);

                                }
                                if (item.lines[l].Angle == item.lines[(l + 3) % item.lines.Count].Angle)
                                {
                                    item.Type = "arrow";
                                    //item.lines[l].ColorIndex = 1;
                                    //    angle = (item.lines[l].Angle - Math.PI / 2)% (Math.PI*2);
                                    //  if (item.lines[(l + 1) % item.lines.Count].EndPoint == item.lines[(l + 2) % item.lines.Count].StartPoint)
                                    virtu.StartPoint = item.lines[(l + 1) % item.lines.Count].EndPoint;
                                    virtu.EndPoint = new Point3d(item.lines[l].StartPoint.X + Math.Cos(item.lines[l].Angle + Math.PI / 2) * 1
                                                               , item.lines[l].StartPoint.Y + Math.Sin(item.lines[l].Angle + Math.PI / 2) * 1, 0);
                                    len1 = virtu.Length;
                                    //virtu.ColorIndex = 1;
                                    //btr.AppendEntity(virtu);
                                    //tr.AddNewlyCreatedDBObject(virtu, true);

                                    virtu.EndPoint = new Point3d(item.lines[l].StartPoint.X + Math.Cos(item.lines[l].Angle - Math.PI / 2) * 1
                                                              , item.lines[l].StartPoint.Y + Math.Sin(item.lines[l].Angle - Math.PI / 2) * 1, 0);
                                    len2 = virtu.Length;
                                    //virtu.ColorIndex = 2;


                                    if (len1 < len2)// here we find the direction of each arrow
                                        angle = item.lines[l].Angle + Math.PI / 2;
                                    else
                                        angle = item.lines[l].Angle - Math.PI / 2;
                                    /*
                                    if ((item.lines[l].Angle + Math.PI / 2 - item.lines[(l + 1) % item.lines.Count].Angle) % (Math.PI * 2) < Math.PI / 2)
                                        angle = (item.lines[l].Angle + Math.PI / 2);
                                    else
                                        angle = (item.lines[l].Angle - (Math.PI / 2));
                                    */

                                    angle = angle % (Math.PI * 2);
                                    item.angles.Add((angle + Math.PI * 2) % (Math.PI * 2));
                                    //item.lines[(l + 3) % item.lines.Count].ColorIndex = 1;

                                }
                            }

                        }

                        foreach (Line linew in item.lines)
                        {// here we give a color for each detected shape
                            if (item.Type == "pedestrian")
                            {
                                linew.ColorIndex = 2;// yellew color

                            }
                            if (item.Type == "arrow")
                            {
                                linew.ColorIndex = 1;// red Color
                            }
                            else
                            {

                            }
                        }

                    }

                    List<Shape> arrowsList = new List<Shape>();
                    List<Shape> pedisList = new List<Shape>();
                    foreach (Shape item in shapes)
                    {
                        if (item.Type == "pedestrian")
                        {
                            pedisList.Add(item);
                        }
                        if (item.Type == "arrow")
                        {
                            arrowsList.Add(item);
                        }
                    }
                    //pedisList = pedisList.OrderBy(item => item.getPosition().X).ToList();
                    //pedisList = pedisList.OrderBy(item => item.getPosition().Y).ToList();
                    double distnce;

                    for (int j = 0; j < pedisList.Count; j++)
                    {
                        Line virtu = new Line();
                        if (pedisList[j] == null)
                            continue;
                        virtu.StartPoint = pedisList[j].getPosition();
                        virtu.EndPoint = pedisList[j].getPosition();

                        Shape currShape = pedisList[j];
                        for (int k = j+1; k < pedisList.Count; k++)
                        {
                            if (pedisList[k] == null)
                                continue;
                            if (Math.Abs(currShape.lines[0].Length + currShape.lines[1].Length +
                                currShape.lines[2].Length + currShape.lines[3].Length -
                                (pedisList[k].lines[0].Length + pedisList[k].lines[1].Length +
                                pedisList[k].lines[2].Length + pedisList[k].lines[3].Length)) <  0.1 && Math.Abs(pedisList[k].lines[0].Length - pedisList[k].lines[1].Length) > 1 &&
                                currShape.getPosition().DistanceTo(pedisList[k].getPosition()) < pedisList[j].lines.Min(line=>line.Length)*3.5)
                            {
                                currShape = pedisList[k];
                                if(virtu.StartPoint.DistanceTo(currShape.getPosition()) > virtu.Length)
                                    virtu.EndPoint = currShape.getPosition();
                                pedisList[k] = null;
                                k = j;

                            }
                        }
                        currShape = pedisList[j];
                        for (int k = j + 1; k < pedisList.Count; k++)
                        {
                            if (pedisList[k] == null)
                                continue;
                            if (Math.Abs(currShape.lines[0].Length + currShape.lines[1].Length +
                                currShape.lines[2].Length + currShape.lines[3].Length -
                                (pedisList[k].lines[0].Length + pedisList[k].lines[1].Length +
                                pedisList[k].lines[2].Length + pedisList[k].lines[3].Length)) < 0.1 && Math.Abs(pedisList[k].lines[0].Length - pedisList[k].lines[1].Length) > 1 &&
                                currShape.getPosition().DistanceTo(pedisList[k].getPosition()) < pedisList[j].lines.Min(line => line.Length) * 3.5)
                            {
                                currShape = pedisList[k];
                                if (virtu.EndPoint.DistanceTo(currShape.getPosition()) > virtu.Length)
                                    virtu.StartPoint = currShape.getPosition();
                                pedisList[k] = null;
                                k = j;

                            }
                        }

                        if (virtu.EndPoint != virtu.StartPoint)
                        {
                            virtu.ColorIndex = 4;
                            btr.AppendEntity(virtu); //drawing the left turns routes
                            tr.AddNewlyCreatedDBObject(virtu, true);
                            List<Line> pedesCollection = new List<Line>();
                            pedesCollection.Add(virtu);
                            pedesConnectors.Add(pedesCollection);
                        }
                    }
                  


                    for (int j = 0; j < arrowsList.Count; j++)
                    {
                        for (int d = 0; arrowsList[j] != null && d < arrowsList[j].angles.Count; d++)
                        {
                            //ed.WriteMessage("\nstartAngle" + item.startAngle);

                            // here we analyze the the routes by the analyzed arrows
                            if (arrowsList[j].getTurnAngle(d) < -0.01)
                                continue;//ignore right turns
                            var dists = new Dictionary<Shape, double>();
                            Line connector = new Line();
                            Shape secondArrow = arrowsList[j];
                            connector.StartPoint = arrowsList[j].getPosition();// this is the the entrance
                            for (int k = 0; k < arrowsList.Count && arrowsList[j] != null; k++)
                            {
                                if (arrowsList[k] == null || arrowsList[k].getTurnAngle(0) < -0.01)
                                    continue;

                                connector.EndPoint = arrowsList[k].getPosition();
                                distnce = arrowsList[j].getPosition().DistanceTo(arrowsList[k].getPosition());
                                if (arrowsList[j].angles[d] == arrowsList[k].angles[0] && distnce > 10 && arrowsList[k].getTurnAngle(0) < 0.01)
                                {
                                    if (Math.Abs(arrowsList[j].angles[d] - connector.Angle) < Math.PI / 2
                                        || Math.Abs(arrowsList[j].angles[d] - connector.Angle) > 3 * Math.PI / 2)
                                        dists.Add(arrowsList[k], distnce);
                                }
                            }
                            if (dists.Count > 0)
                            {

                                
                                
                                double min = 999999;
                                foreach (KeyValuePair<Shape, double> pair in dists)
                                {
                                    if (arrowsList[j].getTurnAngle(d) > 0.01)
                                    {

                                    }
                                    if (pair.Value < min)// && !pair.Key.isVisitedBy(arrowsList[j],d))
                                    {
                                        min = pair.Value;
                                        secondArrow = pair.Key;
                                     //   secondArrow.visitedBy.Add(arrowsList[j].getTurnAngle(d));
                                    }

                                }
                                
                                //secondArrow = dists.Aggregate((x, y) => x.Value < y.Value ? x : y).Key;
                                connector.EndPoint = secondArrow.getPosition();// this is the route exit of the cross
                                connector.ColorIndex = 4;

                                if (arrowsList[j].getTurnAngle(d) > 0.01)//the case of left turn 
                                {
                                    double angle0 = arrowsList[j].startAngle;
                                    double angle1 = secondArrow.startAngle;

                                    double x0 = connector.StartPoint.X;
                                    double y0 = connector.StartPoint.Y;
                                    double m0 = Math.Tan(angle0 % Math.PI);
                                    double x1 = connector.EndPoint.X;
                                    double y1 = connector.EndPoint.Y;
                                    double m1 = Math.Tan(angle1 % Math.PI);
                                    double xTemp, yTemp;
                                    if (angle0 % Math.PI == (Math.PI / 2))
                                        xTemp = x0;
                                    else if (angle1 % Math.PI == (Math.PI / 2))
                                        xTemp = x1;
                                    else
                                        xTemp = ((m0 * x0) - (m1 * x1) + y1 - y0) / (m0 - m1);
                                    if (angle0 % Math.PI != (Math.PI / 2))
                                        yTemp = m0 * (xTemp - x0) + y0;
                                    else
                                        yTemp = y1;
                                    //        double xTemp = (connector.EndPoint.X * Math.Tan(secondArrow.startAngle) -
                                    //      connector.StartPoint.X * Math.Tan(arrowsList[j].startAngle) + connector.EndPoint.Y - connector.StartPoint.Y)
                                    //    / (Math.Tan(arrowsList[j].startAngle) - Math.Tan(secondArrow.startAngle));
                                    //  double yTemp = Math.Tan(arrowsList[j].startAngle) * (xTemp - arrowsList[j].getPosition().X) + arrowsList[j].getPosition().Y;

                                    //Point3d mid = new Point3d(connector.StartPoint.X, connector.EndPoint.Y, 0);
                                    Point3d mid = new Point3d(xTemp, yTemp, 0);
                                    Point3dCollection pntSet = new Point3dCollection
                                    {
                                        connector.StartPoint,
                                        mid,
                                        connector.EndPoint
                                    };
                                    //PolylineCurve3d leftTurn = new PolylineCurve3d(pntSet);
                                    Polyline3d leftTurn = new Polyline3d(Poly3dType.CubicSplinePoly, pntSet, false);
                                    connectors.Add(leftTurn);
                                    curvesPointsColl.Add(pntSet);
                                    

                                    //leftTurn.GetGeCurve().GetDistanceTo(connector.GetGeCurve());
                                    //leftTurn.
                                    leftTurn.ColorIndex = 4;
                                    btr.AppendEntity(leftTurn); //drawing the left turns routes
                                    tr.AddNewlyCreatedDBObject(leftTurn, true);
                                }
                                else
                                {
                                    connectors.Add(connector);
                                    btr.AppendEntity(connector); // drawing the straight routes
                                    tr.AddNewlyCreatedDBObject(connector, true);
                                }
                                if (d == arrowsList[j].angles.Count - 1)
                                    arrowsList[j] = null;
                            }
                        }

                    }

                    var constraints = new List<IConstraint>();
                    for (int i1 = 0; i1 < connectors.Count + pedesConnectors.Count; i1++)
                    {
                        cases.Add(new VariableInteger("" + i1, 0, 10));
                    }

                    for (int i1 = 0; i1 < connectors.Count; i1++)
                    {
                        for (int j1 = i1 + 1; j1 < connectors.Count; j1++)
                        {
                            //ed.WriteMessage("\t" + connectors[i1].GetGeCurve().GetDistanceTo(connectors[j1].GetGeCurve()));
                            
                            if (connectors[i1].StartPoint != connectors[j1].StartPoint
                                && connectors[i1].GetGeCurve().GetDistanceTo(connectors[j1].GetGeCurve()) < 3)
                            {
                                //return;
                                constraints.Add(new ConstraintInteger(cases[i1] != cases[j1]));
                            }
                            else if (connectors[i1].StartPoint == connectors[j1].StartPoint)
                            {
                                constraints.Add(new ConstraintInteger(cases[i1] == cases[j1]));
                            }

                        }

                        for (int i2 = 0; i2 < pedesConnectors.Count; i2++)
                        {
                            List<Line> temp = (List<Line>)(pedesConnectors[i2]);
                            Line connector = temp[0];
                            if (connectors[i1].GetGeCurve().GetDistanceTo(connector.GetGeCurve()) < 3)
                                constraints.Add(new ConstraintInteger(cases[i1] != cases[i2 + connectors.Count]));
                        }
                    }


                    IState<int> state = new StateInteger(cases, constraints);
                    state.StartSearch(out StateOperationResult searchResult);

                    ed.WriteMessage("\n");
                    foreach (VariableInteger aa in cases)
                    {



                    }
                    db.TransactionManager.QueueForGraphicsFlush();
                    /*    
                        tr.Commit();
                    }
                    tr = db.TransactionManager.StartTransaction();
                    using (tr)
                    {
                        tr.GetObject(connectors[i1].Id, OpenMode.ForRead);
                        tr.GetObject(connectors[i1].Id, OpenMode.ForWrite);
                    */

                    List<int> distinctCases = new List<int>();
                    foreach (VariableInteger _case in cases)
                    {
                        distinctCases.Add(_case.Value);
                    }
                    distinctCases = distinctCases.Distinct().ToList();
                    distinctCases.Sort();

                    

                    for (int i1 = 0; i1 < connectors.Count; i1++)
                    {

                        connectors[i1].ColorIndex = 3 + cases[i1].Value;
                    }

                    for (int i1 = 0; i1 < pedesConnectors.Count; i1++)
                    {
                        List<Line> temp = (List<Line>)(pedesConnectors[i1]);

                        temp[0].ColorIndex = 3 + cases[i1 + connectors.Count].Value;
                    }
                    List<int> toRemove = new List<int>();
                    foreach(List<Line> pedesCol in pedesConnectors)
                    {
                        Line pedes = pedesCol[0];
                        List<bool> availableCases = new List<bool>();
                        for (int i1 = 0; i1 < distinctCases.Count; i1++)
                        {
                            availableCases.Add(true);
                        }

                        bool free = true;
                        for (int i2 = 0; i2 < connectors.Count; i2++)
                        {
                            Curve connector = connectors[i2];
                            if (pedes.GetGeCurve().GetDistanceTo(connector.GetGeCurve()) < 3)
                            {
                                availableCases[connector.ColorIndex - 3] = false;
                                free = false;
                            }
                        }
                        Line current = pedes;
                        for (int i2 = 0; i2 < availableCases.Count; i2++)
                            if (availableCases[i2] && pedes.ColorIndex != 3+i2 && !free)
                            {
                                Point3d additionStart = new Point3d(pedes.StartPoint.X + i2* Math.Cos(pedes.Angle),
                                                                pedes.StartPoint.Y +  i2 * Math.Sin(pedes.Angle), 0);
                                Point3d additionEnd = new Point3d(pedes.EndPoint.X + i2 * Math.Cos(pedes.Angle),
                                                                    pedes.EndPoint.Y + i2 * Math.Sin(pedes.Angle), 0);
                                DBObjectCollection acDbObjColl = current.GetOffsetCurves(0.5);
                                Line available = (Line)acDbObjColl[0];
                                current = available;
                                available.ColorIndex = 3 + i2;
                                pedesCol.Add(available);
                                btr.AppendEntity(available); // drawing the straight routes
                                tr.AddNewlyCreatedDBObject(available, true);
                            }

                        if (free)
                        {
                            pedes.Erase(true);
                            int i1;
                            for (i1 = 0; i1 < pedesConnectors.Count; i1++)
                            {
                                List<Line> forRemove = (List<Line>)pedesConnectors[i1];
                                if (forRemove[0] == pedes)
                                {
                                    toRemove.Add(i1);
                                    break;
                                }
                            }
                            
                        }
                    }
                    for(int i3 = toRemove.Count - 1;i3 >=0;i3--)
                    {
                        pedesConnectors.RemoveAt(toRemove[i3]);
                    }

                    db.TransactionManager.QueueForGraphicsFlush();

                    PromptDoubleOptions loadOptions = new PromptDoubleOptions("")
                    {
                        DefaultValue = 1,
                        AllowNegative = false,
                        AllowZero = false
                    };

                    ed.WriteMessage("\nPlease enter the total load ");
                    
                    List<double> loads = new List<double>();
                    foreach (int _case in distinctCases)
                    {
                        loadOptions.Message = "for " + Color.FromColorIndex(ColorMethod.ByColor, (short)(_case + 3)).ColorNameForDisplay + "\n";
                        loads.Add(ed.GetDouble(loadOptions).Value);
                        //var a = Color.FromColorIndex(ColorMethod.ByAci, (short)(_case.Value + 3)).ColorName;
                    }

                    ed.WriteMessage("We found " + distinctCases.Count + " phases:-\n");
                    int caseCapacity;
                    List<double> caseLengthAvg = new List<double>();
                    List<int> caseGreenTime = new List<int>(); // seconds

                    foreach (int _case in distinctCases)
                    {// loadAvg * lengthAvg / 10

                        caseCapacity = 0;
                        caseLengthAvg.Add(0);
                        foreach (Curve connector in connectors)
                        {
                            if(connector.ColorIndex == 3 + _case)
                            {
                                caseCapacity++;
                                caseLengthAvg[_case] += connector.GetDistanceAtParameter(connector.EndParam - connector.StartParam);
                                //int w = 0;
                            }
                            
                        }
                        caseLengthAvg[_case] /= caseCapacity;
                        loads[_case] /= caseCapacity;
                        caseGreenTime.Add((int) (caseLengthAvg[_case] * loads[_case] / 40)+5);

                    }

                    ed.WriteMessage("\nPlease enter the needed time ");
                    foreach (List<Line> pedesCol in pedesConnectors)
                    {
                        string colour = "";
                        foreach(Line pedes in pedesCol)
                        {
                            colour = colour + Color.FromColorIndex(ColorMethod.ByColor, (short)(pedes.ColorIndex)).ColorNameForDisplay + " ";
                        }
                        loadOptions.Message = "for the " + colour + "pedestration routes\n";
                        int t = (int)ed.GetDouble(loadOptions).Value;
                        int totalTime = 0;
                        foreach (Line pedes in pedesCol)
                        {
                            totalTime += caseGreenTime[pedes.ColorIndex - 3];
                        }
                        if (t > totalTime)
                        {
                            foreach (Line pedes in pedesCol)
                            {
                                caseGreenTime[pedes.ColorIndex - 3] += (t - totalTime) / pedesCol.Count;
                            }
                        }
                        //var a = Color.FromColorIndex(ColorMethod.ByAci, (short)(_case.Value + 3)).ColorName;
                    }
                    if (caseGreenTime.Count > 0)
                    {
                        double ratio = (double)120 / (caseGreenTime.Sum() - caseGreenTime.Min()); // in order to resrict waiting time from being more than 120
                        if (ratio < 1)
                        {
                            foreach (int _case in distinctCases)
                            {
                                caseGreenTime[_case] = (int)(caseGreenTime[_case] * ratio);
                            }
                        }
                    }
                    int j2 = 0;
                    foreach (Curve connector in connectors)
                    {
                        var connectorType = connector.GetType();
                        if (connectorType.Name == "Line")
                        {
                            routesToWrite = routesToWrite + "\t<route>" + connector.StartPoint + ", " + connector.EndPoint + ", " +
                                loads[connector.ColorIndex - 3] + "," + (connector.ColorIndex-3) +" </route>\n";
                        }
                        else
                        {
                            routesToWrite = routesToWrite + "\t<route>" + curvesPointsColl[j2][0] + ", " + curvesPointsColl[j2][1] + ", "
                                + curvesPointsColl[j2][2] + ", " + loads[connector.ColorIndex - 3] + "," + (connector.ColorIndex - 3) + " </route>\n";
                            j2++;
                        }
                    }
                    routesToWrite = routesToWrite + "</routes>\n";

                    foreach(List<Line> pedesCol in pedesConnectors)
                    {
                        pedesRoutesToWrite = pedesRoutesToWrite + "\t<route>" + pedesCol[0].StartPoint + ", " + pedesCol[0].EndPoint + ", " +
                                caseGreenTime[pedesCol[0].ColorIndex - 3] + "," + (pedesCol[0].ColorIndex - 3) + " </route>\n";
                    }

                    pedesRoutesToWrite = pedesRoutesToWrite + "</pedestrain_routes>\n";

                    int[] arr = distinctCases.ToArray();
                    int[] green = caseGreenTime.ToArray();

                    int temp1, temp2;
                    for (int j = 0; j <= arr.Length - 2; j++)
                    {
                        for (int i3 = 0; i3 <= arr.Length - 2; i3++)
                        {
                            if (green[i3] < green[i3 + 1])
                            {
                                temp1 = arr[i3 + 1];
                                arr[i3 + 1] = arr[i3];
                                arr[i3] = temp1;
                                temp2 = green[i3 + 1];
                                green[i3 + 1] = green[i3];
                                green[i3] = temp2;
                            }
                        }
                    }

                    foreach (int _case in arr)
                    {
                        ed.WriteMessage("for the " + Color.FromColorIndex(ColorMethod.ByColor, (short)(_case + 3)).ColorNameForDisplay +
                                                                " routes we need: " + caseGreenTime[_case] + " seconds\n");
                    }

                    PromptKeywordOptions KeyOptions = new PromptKeywordOptions("")
                    {
                        Message = "Would you like to export the data? \n",
                        AllowNone = false
                    };
                    KeyOptions.Keywords.Add("Yes");
                    KeyOptions.Keywords.Add("No");
                    PromptResult KeyRes = ed.GetKeywords(KeyOptions);
                    if (KeyRes.StringResult == "Yes")
                    {
                        SaveFileDialog sfd = new SaveFileDialog("Save schedule calculating data file results", null, "xml", "DataFileToCalculate",
                                     SaveFileDialog.SaveFileDialogFlags.DoNotTransferRemoteFiles);
                        
                        if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        {
                            File.WriteAllText(sfd.Filename, routesToWrite + "\n" + pedesRoutesToWrite);
                        }
                        
                    }
                    tr.Commit();
                }
                // There are selected entities
                // Put your command using pickfirst set code here
            }
            else
            {
                // There are no selected entities
                // Put your command code here
            }
        }

        // Application Session Command with localized name
        [CommandMethod("MyGroup", "MySessionCmd", "MySessionCmdLocal", CommandFlags.Modal | CommandFlags.Session)]
        public void MySessionCmd() // This method can have any name
        {
            System.Console.WriteLine("Hello World! 2");            // Put your command code here
        }

        // LispFunction is similar to CommandMethod but it creates a lisp 
        // callable function. Many return types are supported not just string
        // or integer.
        [LispFunction("MyLispFunction", "MyLispFunctionLocal")]
        public int MyLispFunction(ResultBuffer args) // This method can have any name
        {
            // Put your command code here

            // Return a value to the AutoCAD Lisp Interpreter
            return 1;
        }

        /*
        private List<Line> FindLine(Point3d a, List<Line> list)
        {
            List<Line> found = new List<Line>();
            foreach (Line line in list)
            {
                if (a == line.StartPoint)
                {
                    found.Add(line);
                }
                return found;
            }
        }
        */
    }

    public class Shape
    {
        public List<Line> getLines()
        {
            return lines;
        }
        public Point3d getPosition()//this function returns the center of the shape coordinates
        {
            double positionX = 0;
            double positionY = 0;
            foreach (Line line in lines)
            {
                positionX += line.StartPoint.X;
                positionX += line.EndPoint.X;
                positionY += line.StartPoint.Y;
                positionY += line.EndPoint.Y;

            }
            return new Point3d(positionX / (lines.Count*2), positionY / (lines.Count * 2), 0);
        }

        public double getTurnAngle(int dirIndex)
        {// returns positive if left and negativee for right
            double turnAngle = angles[dirIndex] - startAngle;
            if (turnAngle < -1 * Math.PI)
                return turnAngle + 2*Math.PI;
            else if (turnAngle > Math.PI)
                return -1 * (2*Math.PI - turnAngle);

            return turnAngle;
        }

        public bool isVisitedBy(Shape arrow, int dir)
        {
            foreach(double d in visitedBy)
            { double angle = arrow.getTurnAngle(dir);
                if (d == angle)
                {
                    return true;
                }
                    
            }
            return false;

        }

        public List<Line> lines = new List<Line>();
        public List<double> visitedBy = new List<double>();
        public int height;
        public string Type;
        public double startAngle;
        public int load;
        public List<double> angles = new List<double>();
    }


    // Derived class
    public class ArrowShape : Shape
    {
        public int GetDirection()
        {
            return height;
        }
        public Boolean IsTurn()
        {
            return false;
        }
        public List<Line> headLines;
    }


}