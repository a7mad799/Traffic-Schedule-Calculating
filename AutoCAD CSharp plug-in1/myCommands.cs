﻿// (C) Copyright 2019 by  
//
using System;
using System.Diagnostics;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using System.Collections.Generic;

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
                            Line line = (Line)tr.GetObject(so.ObjectId, OpenMode.ForRead);
                            myLines.Add(line);


                            if (!firstline) {
                                distance = line.StartPoint.X - prevLine.StartPoint.X;
                            }
                            //ed.WriteMessage("\nStartPoint: {0} \nEndPoint: {1}\nsteps: {2}", line.StartPoint, line.EndPoint, distance);
                            prevLine = line ;
                            firstline = false;
                        }
                       
                    }

                    Line curr = new Line();
                    Line line1 = new Line();
                    Line line2 = new Line();
                    int i, foundLines, checkCount, nullCount = 0;
                    for(int l = 0; l<myLines.Count ;l++)
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
                                    if (curr.EndPoint == line2.StartPoint && line1 != line2)
                                    {
                                        checkCount = 0;
                                        foundLines++;
                                        curr.EndPoint = line2.EndPoint;
                                        ed.WriteMessage(foundLines + "\t");
                                        newShape.lines.Add(myLines[i]);
                                        myLines[i] = null;
                                    }
                                    else if (curr.EndPoint == line2.EndPoint && line1 != line2)
                                    {
                                        checkCount = 0;
                                        foundLines++;
                                        curr.EndPoint = line2.StartPoint;
                                        ed.WriteMessage(foundLines + "\t");
                                       
                                        newShape.lines.Add(myLines[i]);
                                        myLines[i] = null;
                                    }
                                    if (foundLines == 100)
                                    {
                                        newShape = null;
                                        ed.WriteMessage("\ninfinite loop");
                                        break;
                                    }
                                }
                                if (checkCount >= 2 * myLines.Count)
                                {
                                    newShape = null;
                                    checkCount = 0;
                                    ed.WriteMessage("\ninfinite checks");
                                    break;
                                }
                              

                                
                                //break;  
                                i = (i + 1) % myLines.Count;
                            }
                            if (newShape != null)
                            {
                                shapes.Add(newShape);
                            }
                        }


                    }

                    foreach (Shape item in shapes)
                    {
                        foreach (Line linew in item.lines)
                        {
                            ed.WriteMessage("\nstart point" + linew.StartPoint);

                        }
                    }
                    //ed.WriteMessage("\n" + arrowsLines.Count);
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
        public Point3d getPosition(int h)
        {
            return lines[0].StartPoint;
        }
        public List<Line> lines = new List<Line>();
        public int height;
    }


    // Derived class
    public class ArrowShape : Shape
    {
        public int getDirection()
        {
            return height;
        }
        public Boolean isTurn()
        {
            return false;
        }
        public List<Line> headLines;
    }

}
