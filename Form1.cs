using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;

namespace CustomSoftBodyMaker
{
    public class SoftBodyForm : Form
    {
        private List<List<PointF>> drawnShapes = new List<List<PointF>>();
        private List<PointF> currentShape = new List<PointF>();
        private List<SoftBody> softBodies = new List<SoftBody>();
        private bool simulationMode = false;
        private System.Windows.Forms.Timer simulationTimer;
        private Button renderButton;
        private Button clearButton;
        private MassPoint selectedPoint = null;
        private bool isDragging = false;

        public SoftBodyForm()
        {
            this.Width = 1000;
            this.Height = 1000;
            this.Text = "Custom Soft Body Maker";
            this.DoubleBuffered = true;

            renderButton = new Button() { Text = "Render", Location = new Point(10, 10) };
            renderButton.Click += RenderButton_Click;
            this.Controls.Add(renderButton);

            clearButton = new Button() { Text = "Clear", Location = new Point(100, 10) };
            clearButton.Click += ClearButton_Click;
            this.Controls.Add(clearButton);

            this.MouseDown += SoftBodyForm_MouseDown;
            this.MouseMove += SoftBodyForm_MouseMove;
            this.MouseUp += SoftBodyForm_MouseUp;
            this.Paint += SoftBodyForm_Paint;

            simulationTimer = new System.Windows.Forms.Timer { Interval = 16 };
            simulationTimer.Tick += SimulationTimer_Tick;
        }

        private void SoftBodyForm_MouseDown(object sender, MouseEventArgs e) // Add points to the current shape
        {
            if (simulationMode)
            {
                foreach (var body in softBodies)
                {
                    foreach (var point in body.Points)
                    {
                        if (IsPointClose(new PointF(point.Position.X, point.Position.Y), new PointF(e.X, e.Y)))
                        {
                            selectedPoint = point;
                            isDragging = true;
                            return;
                        }
                    }
                }
            }
            else
            {
                PointF pt = new PointF(e.X, e.Y);
                if (currentShape.Count > 0 && IsPointClose(currentShape[0], pt))
                {
                    if (currentShape.Count > 2) drawnShapes.Add(new List<PointF>(currentShape));
                    currentShape.Clear();
                }
                else
                {
                    currentShape.Add(pt);
                }
                Invalidate();
            }
        }

        private void SoftBodyForm_MouseMove(object sender, MouseEventArgs e) // Drag points
        {
            if (isDragging && selectedPoint != null)
            {
                selectedPoint.Position = new Vector2(e.X, e.Y);
                Invalidate();
            }
        }

        private void SoftBodyForm_MouseUp(object sender, MouseEventArgs e) // Stop dragging
        {
            isDragging = false;
            selectedPoint = null;
        }

        private bool IsPointClose(PointF a, PointF b, float threshold = 10f)
        {
            return (Math.Abs(a.X - b.X) < threshold && Math.Abs(a.Y - b.Y) < threshold);
        }

        private void SoftBodyForm_Paint(object sender, PaintEventArgs e) // Draw shapes and bodies
        {
            Graphics g = e.Graphics;
            if (!simulationMode)
            {
                foreach (var shape in drawnShapes)
                {
                    if (shape.Count > 1)
                    {
                        g.DrawPolygon(Pens.Blue, shape.ToArray());
                        foreach (var pt in shape)
                        {
                            g.FillEllipse(Brushes.Blue, pt.X - 3, pt.Y - 3, 6, 6);
                        }
                    }
                }

                if (currentShape.Count > 1)
                {
                    g.DrawLines(Pens.Black, currentShape.ToArray());
                    foreach (var pt in currentShape)
                    {
                        g.FillEllipse(Brushes.Black, pt.X - 3, pt.Y - 3, 6, 6);
                    }
                }
            }
            else
            {
                foreach (var body in softBodies) body.Draw(g);
            }
        }

        private void RenderButton_Click(object sender, EventArgs e)
        {
            if (!simulationMode)
            {
                softBodies.Clear();
                const int maxBodies = 10; // Limit the number of bodies
                int count = 0;

                foreach (var shape in drawnShapes)
                {
                    if (count >= maxBodies) break;
                    softBodies.Add(SoftBody.CreateFromPolygon(shape));
                    count++;
                }

                if (currentShape.Count > 2 && count < maxBodies)
                {
                    softBodies.Add(SoftBody.CreateFromPolygon(currentShape));
                    currentShape.Clear();
                }

                simulationMode = true;
                simulationTimer.Start();
            }
        }

        private void ClearButton_Click(object sender, EventArgs e)
        {
            simulationTimer.Stop();
            drawnShapes.Clear();
            currentShape.Clear();
            softBodies.Clear();
            simulationMode = false;
            isDragging = false;
            selectedPoint = null;
            Invalidate();
        }

        private void SimulationTimer_Tick(object sender, EventArgs e)
        {
            float dt = 0.016f;
            foreach (var body in softBodies) body.Update(dt);
            Invalidate();
        }

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.Run(new SoftBodyForm());
        }
    }

    public class SoftBody
    {
        public List<MassPoint> Points { get; private set; } = new List<MassPoint>(); // Points and Springs
        public List<Spring> Springs { get; private set; } = new List<Spring>();

        public static SoftBody CreateFromPolygon(List<PointF> polygon)
        {
            SoftBody body = new SoftBody();
            int n = polygon.Count;
            if (n < 3) return body;

            // Create mass points for the original polygon
            foreach (var pt in polygon)
                body.Points.Add(new MassPoint(new Vector2(pt.X, pt.Y)));

            // Create springs for the original polygon edges
            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                body.Springs.Add(new Spring(body.Points[i], body.Points[next]));
            }

            // Add subdivisions
            List<MassPoint> newPoints = new List<MassPoint>();
            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                Vector2 midPoint = (body.Points[i].Position + body.Points[next].Position) / 2;
                MassPoint midMassPoint = new MassPoint(midPoint);
                newPoints.Add(midMassPoint);
                body.Points.Add(midMassPoint);

                // Create springs between original points and new midpoints
                body.Springs.Add(new Spring(body.Points[i], midMassPoint));
                body.Springs.Add(new Spring(midMassPoint, body.Points[next]));
            }

            // Create springs between new midpoints
            for (int i = 0; i < newPoints.Count; i++)
            {
                int next = (i + 1) % newPoints.Count;
                body.Springs.Add(new Spring(newPoints[i], newPoints[next]));
            }

            // Create a center point and connect it to all points
            Vector2 centroid = Vector2.Zero;
            foreach (var pt in polygon) centroid += new Vector2(pt.X, pt.Y);
            centroid /= n;

            MassPoint centerPoint = new MassPoint(centroid);
            body.Points.Add(centerPoint);

            foreach (var point in body.Points)
                body.Springs.Add(new Spring(point, centerPoint));

            return body;
        }

        public void Update(float dt)
        {
            // Reset forces and apply gravity
            foreach (var p in Points)
                p.Force = Vector2.Zero;
            Vector2 gravity = new Vector2(0, 980f);
            foreach (var p in Points)
                if (!p.IsPinned)
                    p.Force += gravity * p.Mass;

            // Apply spring forces
            foreach (var s in Springs)
                s.ApplyForce();

            // Update point positions based on forces
            foreach (var p in Points)
                if (!p.IsPinned)
                    p.Update(dt);

            // Enforce constraints multiple times for stability:
            // this will keep each spring from stretching or compressing too much.
            for (int i = 0; i < 3; i++)
            {
                EnforceConstraints();
            }
        }

        private void EnforceConstraints()
        {
            foreach (var s in Springs)
            {
                Vector2 delta = s.B.Position - s.A.Position;
                float currentLength = delta.Length();
                if (currentLength == 0) continue;

                // Set the allowed minimum and maximum length as a fraction of RestLength
                float minLength = s.RestLength * 0.5f;
                float maxLength = s.RestLength * 1.5f;

                if (currentLength > maxLength)
                {
                    float excess = currentLength - maxLength;
                    Vector2 correction = delta / currentLength * excess;
                    if (!s.A.IsPinned && !s.B.IsPinned)
                    {
                        s.A.Position += correction * 0.5f;
                        s.B.Position -= correction * 0.5f;
                    }
                    else if (!s.A.IsPinned)
                    {
                        s.A.Position += correction;
                    }
                    else if (!s.B.IsPinned)
                    {
                        s.B.Position -= correction;
                    }
                }
                else if (currentLength < minLength)
                {
                    float deficit = minLength - currentLength;
                    Vector2 correction = delta / currentLength * deficit;
                    if (!s.A.IsPinned && !s.B.IsPinned)
                    {
                        s.A.Position -= correction * 0.5f;
                        s.B.Position += correction * 0.5f;
                    }
                    else if (!s.A.IsPinned)
                    {
                        s.A.Position -= correction;
                    }
                    else if (!s.B.IsPinned)
                    {
                        s.B.Position += correction;
                    }
                }
            }
        }

        public void Draw(Graphics g)
        {
            foreach (var s in Springs)
            {
                Point a = new Point((int)s.A.Position.X, (int)s.A.Position.Y);
                Point b = new Point((int)s.B.Position.X, (int)s.B.Position.Y);
                g.DrawLine(Pens.Black, a, b);
            }

            foreach (var p in Points)
            {
                int radius = 4;
                g.FillEllipse(Brushes.Red, p.Position.X - radius, p.Position.Y - radius, radius * 2, radius * 2);
            }
        }
    }

    public class MassPoint
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public Vector2 Force;
        public float Mass;
        public bool IsPinned;

        public MassPoint(Vector2 pos, float mass = 1f)
        {
            Position = pos;
            Mass = mass;
            Velocity = Vector2.Zero;
            Force = Vector2.Zero;
            IsPinned = false;
        }

        public void Update(float dt)
        {
            Vector2 acceleration = Force / Mass;
            Velocity += acceleration * dt;

            float maxVelocity = 500f;
            if (Velocity.Length() > maxVelocity)
                Velocity = Vector2.Normalize(Velocity) * maxVelocity;

            // Apply damping to simulate friction/air resistance
            Velocity *= 0.98f;
            Position += Velocity * dt;

            // Clamp within the screen bounds
            float minX = 0, maxX = 800, minY = 0, maxY = 600;
            Position.X = Math.Clamp(Position.X, minX, maxX);
            Position.Y = Math.Clamp(Position.Y, minY, maxY);
        }
    }

    public class Spring
    {
        public MassPoint A;
        public MassPoint B;
        public float RestLength;
        public float Stiffness;
        public float Damping;

        public Spring(MassPoint a, MassPoint b, float stiffness = 10f, float damping = 10f)
        {
            A = a;
            B = b;
            RestLength = Vector2.Distance(a.Position, b.Position);
            Stiffness = stiffness;
            Damping = damping;
        }

        public void ApplyForce()
        {
            Vector2 delta = B.Position - A.Position;
            float currentLength = delta.Length();
            if (currentLength == 0) return;

            Vector2 direction = delta / currentLength;
            float displacement = currentLength - RestLength;
            Vector2 springForce = -Stiffness * displacement * direction;

            float maxForce = 10000f; // Limit the maximum force
            if (springForce.Length() > maxForce)
                springForce = Vector2.Normalize(springForce) * maxForce;

            Vector2 dampingForce = -Damping * Vector2.Dot(B.Velocity - A.Velocity, direction) * direction;
            Vector2 totalForce = springForce + dampingForce;

            if (!A.IsPinned) A.Force += totalForce;
            if (!B.IsPinned) B.Force -= totalForce;
        }
    }
}
