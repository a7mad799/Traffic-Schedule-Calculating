// (C) Copyright 2019 by  
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
                    ed.WriteMessage("hello\n");
                }
                Transaction tr = db.TransactionManager.StartTransaction();
                using (tr)
                {
                    bool firstline = true;
                    Line prevLine = null;
                    double distance = 0;
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                    RXClass rxclass = RXClass.GetClass(typeof(Line));
                    int count = 0;
                    List<Line> myLines = new List<Line>();
                    foreach (SelectedObject so in psr.Value)
                    {
                        
                        if (so.ObjectId.ObjectClass.IsDerivedFrom(rxclass))
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
                    List<Line> arrowsLines = new List<Line>();
                    bool newLine;
                    //myLines.Sort();
                    foreach (Line line1 in myLines)
                    {
                        newLine = true;
                        foreach (Line line2 in myLines)
                        {
                            if(line1.EndPoint == line2.EndPoint && !(line1.StartPoint == line2.StartPoint && line1.EndPoint == line2.EndPoint))
                            {
                                for (int i=0; i<arrowsLines.Count; i++)
                                {
                                    if(arrowsLines[i].EndPoint == line1.EndPoint)
                                    {
                                        newLine = false;
                                    }
                                }
                                if (newLine)
                                {
                                    arrowsLines.Add(line1);
                                    count++;
                                }
                                
                            }
                        }
                    }
                    Line curr;
                    int foundLines;
                    List<Line> nearLines;
                    foreach (Line line1 in arrowsLines)
                    {
                        foundLines = 0;
                        curr = new Line();
                        curr.EndPoint = line1.EndPoint;
                        nearLines = new List<Line>();
                        foreach (Line line3 in myLines)
                        {
                            curr.StartPoint = line3.StartPoint;
                            if(curr.Length < 4)
                            {
                                nearLines.Add(line3);
                            }
                        }
                        int i = 0;
                        Line line2;
                        while (curr.EndPoint == line1.EndPoint)
                        {
                            line2 = nearLines[i];
                            if (curr.EndPoint == line2.StartPoint && line1 != line2)
                            {
                                foundLines++;
                                curr.EndPoint = line2.EndPoint;
                                ed.WriteMessage(foundLines + "\t");
                            }
                            else if (curr.EndPoint == line2.EndPoint && line1 != line2)
                            {
                                foundLines++;
                                curr.EndPoint = line2.StartPoint;
                                ed.WriteMessage(foundLines + "\t");


                            }
                            if (foundLines == 100)
                            {
                                ed.WriteMessage("\ninfinite loop");
                                break;
                            }
                            i=(i+1)%nearLines.Count;
                        }
                            
                        if (foundLines == 7 || foundLines == 13 || foundLines == 8)// infinite looooooooooooooop do not start
                        {
                            ed.WriteMessage("\nreal line");
                        }
                        
                    }                           

                    ed.WriteMessage("\n" + arrowsLines.Count);
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

}
