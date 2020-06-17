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
        [CommandMethod("MyGroup", "MyCommand", "MyCommandLocal", CommandFlags.Modal)]
        public void MyCommand() // This method can have any name
        {
            // Put your command code here
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed;
            if (doc != null)
            {
                ed = doc.Editor;
                
                Debug.WriteLine("myCommand called");

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
                Transaction tr = db.TransactionManager.StartTransaction();
                List<Curve> connectors = new List<Curve>();

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
                    foreach (Shape item in shapes)
                    {
                        if (item.Type == "arrow")
                        {
                            arrowsList.Add(item);
                        }
                    }

                    double distnce;


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
                            Shape secondArrow = new Shape();
                            connector.StartPoint = arrowsList[j].getPosition();// this is the the entrance
                            for (int k = 0; k < arrowsList.Count && arrowsList[j] != null; k++)
                            {
                                if (arrowsList[k] == null || arrowsList[k].getTurnAngle(0) < -0.01)
                                    continue;

                                connector.EndPoint = arrowsList[k].getPosition();
                                distnce = arrowsList[j].getPosition().DistanceTo(arrowsList[k].getPosition());
                                if (arrowsList[j].angles[d] == arrowsList[k].angles[0] && distnce > 10)
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
                                    if (pair.Value < min)
                                    {
                                        min = pair.Value;
                                        secondArrow = pair.Key;
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
                                    Point3dCollection pntSet = new Point3dCollection();
                                    pntSet.Add(connector.StartPoint);
                                    pntSet.Add(mid);
                                    pntSet.Add(connector.EndPoint);
                                    //PolylineCurve3d leftTurn = new PolylineCurve3d(pntSet);
                                    Polyline3d leftTurn = new Polyline3d(Poly3dType.CubicSplinePoly, pntSet, false);
                                    connectors.Add(leftTurn);
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
                    for (int i1 = 0; i1 < connectors.Count; i1++)
                    {
                        cases.Add(new VariableInteger("" + i1, 0, 5));
                    }

                    for (int i1 = 0; i1 < connectors.Count; i1++)
                    {
                        for (int j1 = i1 + 1; j1 < connectors.Count; j1++)
                        {
                            //ed.WriteMessage("\t" + connectors[i1].GetGeCurve().GetDistanceTo(connectors[j1].GetGeCurve()));
                            if (connectors[i1].StartPoint != connectors[j1].StartPoint
                                && connectors[i1].GetGeCurve().GetDistanceTo(connectors[j1].GetGeCurve()) < 3)
                            {
                                constraints.Add(new ConstraintInteger(cases[i1] != cases[j1]));
                            }
                            else if (connectors[i1].StartPoint == connectors[j1].StartPoint)
                            {
                                constraints.Add(new ConstraintInteger(cases[i1] == cases[j1]));
                            }

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

                    for (int i1 = 0; i1 < connectors.Count; i1++)
                    {

                        connectors[i1].ColorIndex = 3 + cases[i1].Value;
                    }
                    db.TransactionManager.QueueForGraphicsFlush();

                    PromptDoubleOptions loadOptions = new PromptDoubleOptions("");
                    loadOptions.DefaultValue = 1;
                    loadOptions.AllowNegative = false;
                    loadOptions.AllowZero = false;



                    ed.WriteMessage("\nPlease enter the total load ");
                    List<int> distinctCases = new List<int>();
                    foreach (VariableInteger _case in cases)
                    {
                        distinctCases.Add(_case.Value);
                    }
                    distinctCases = distinctCases.Distinct().ToList();
                    distinctCases.Sort();
                    List<double> loads = new List<double>();
                    foreach (int _case in distinctCases)
                    {
                        loadOptions.Message = "for " + Color.FromColorIndex(ColorMethod.ByColor, (short)(_case + 3)).ColorNameForDisplay + "\n";
                        loads.Add(ed.GetDouble(loadOptions).Value);
                        //var a = Color.FromColorIndex(ColorMethod.ByAci, (short)(_case.Value + 3)).ColorName;
                    }

                    double load = 5;

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
                        caseGreenTime.Add((int) (caseLengthAvg[_case] * loads[_case] / 10));

                    }
                    double ratio = (double)120/(caseGreenTime.Sum() - caseGreenTime.Min()); // in order to resrict waiting time from being more than 120
                    if (ratio < 1)
                    {
                        foreach (int _case in distinctCases)
                        {
                            caseGreenTime[_case] = (int)(caseGreenTime[_case] * ratio);
                        }
                    }
                    int w = 5;

                    foreach (int _case in distinctCases)
                    {
                        ed.WriteMessage("for " + Color.FromColorIndex(ColorMethod.ByColor, (short)(_case + 3)).ColorNameForDisplay +
                                                                "color we need: " + caseGreenTime[_case] + " seconds\n");
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


        public List<Line> lines = new List<Line>();
        public int height;
        public string Type;
        public double startAngle;
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