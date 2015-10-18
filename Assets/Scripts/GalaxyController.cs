// Credits:
// Math and Galaxy stuff from: https://github.com/beltoforion/Galaxy-Renderer
// Article here: http://articles.beltoforion.de/article.php?a=spiral_galaxy_renderer&hl=en
//
using System;
using UnityEngine;
using System.Collections.Generic;

public class GalaxyController : MonoBehaviour {
    public GameObject nodePrefab;
    public GameObject dustPrefab;
    public GameObject H2Prefab;

    private DateTime lastUpdate = new DateTime();
    private List<string> commands = new List<string>();         // Local copy of commands received, this list is emptied once treated.
    private List<GameObject> nodes = new List<GameObject>();    // Nodes created from prefab
    private List<GameObject> dust = new List<GameObject>();     // dust created from prefab
    private List<GameObject> h2Stars = new List<GameObject>();     // dust created from prefab
    MainController mainController;                              // Main controller managing received commands and UI

    // Math and Galaxy generation declaration
    int RAND_MAX = 32767;
    double PC_TO_KM = 3.08567758129e13;
    double SEC_PER_YEAR = 365.25 * 86400;

    // Parameters needed for defining the general structure of the galaxy

    double m_elEx1;          ///< Excentricity of the innermost ellipse
    double m_elEx2;          ///< Excentricity of the outermost ellipse

    //double m_velOrigin = 30;      ///< Velovity at the innermost core in km/s
    //double m_velInner;       ///< Velocity at the core edge in km/s
    //double m_velOuter;       ///< Velocity at the edge of the disk in km/s

    double m_angleOffset;    ///< Angular offset per parsec

    double m_radCore;        ///< Radius of the inner core
    double m_radGalaxy;      ///< Radius of the galaxy
    double m_radFarField;    ///< The radius after which all density waves must have circular shape
    //double m_sigma = 0.45;          ///< Distribution of stars
    //double m_velAngle;       ///< Angular velocity of the density waves
    int m_numStars;          ///< Total number of stars
    int m_numDust;           ///< Number of Dust Particles
    int m_numH2 = 300;       ///< Number of H2 Regions
    double rad;
    double m_time;
    //double m_timeStep;


    //int[] m_numberByRad = new int[100];  ///< Historgramm showing distribution of stars

    Vector2 m_pos;             ///< Center of the galaxy
    List<Star> m_pStars = new List<Star>();
    List<Star> m_pDust = new List<Star>();
    List<Star> m_pH2 = new List<Star>();

    private void Start()
    {
        mainController = MainController.Instance;
        mainController.DisplayIsUpdating(false);
        if (mainController != null)
        {
            foreach (string c in mainController.commands)
            {
                commands.Add(c);
            }
        }
    }

    // Update is called once per frame
    void Update ()
    {
        // Check if new commands arrived 
        if (mainController && lastUpdate != mainController.lastUpdate)
        {
            commands.RemoveAll(AllCommands);

            mainController.DisplayIsUpdating(false);
            foreach (string c in mainController.commands)
            {
                commands.Add(c);
            }
            lastUpdate = mainController.lastUpdate;
        }

        // Deal with received commands
        foreach (string elem in commands)
        {
            string[] data = elem.Split(';');

            if (data.Length > 1)
            {
                string command = data[0];

                if (command == "GALAXY_1.0")
                {
                    string name = data[1];
                    string starCount = data[2];
                    string description = data[3];
                    string galaxyRadius = data[4];
                    string coreRadius = data[5];
                    string angularOffset = data[6];
                    string coreExcentricity = data[7];
                    string edgeExcentricity = data[8];
//                    string sigma = data[9];
//                    string coreOrbitalVelocity = data[10];
//                    string edgeOrbitalVelocity = data[11];
                    string dustCount = data[9];

                    if (int.Parse(starCount) != m_pStars.Count || int.Parse(dustCount) != m_pDust.Count)
                    {
                        foreach (GameObject g in h2Stars)
                        {
                            h2Stars.Remove(g);
                            Destroy(g);
                        }
                        foreach (GameObject g in h2Stars)
                        {
                            h2Stars.Remove(g);
                            Destroy(g);
                        }
                        foreach (GameObject g in dust)
                        {
                            nodes.Remove(g);
                            Destroy(g);
                        }

                        NewGalaxy(int.Parse(galaxyRadius),            // radius of the galaxy
                                    int.Parse(coreRadius),              // radius of the core
                                    double.Parse(angularOffset),        // angular offset of the density wave per parsec of radius
                                    double.Parse(coreExcentricity),     // excentricity at the edge of the core
                                    double.Parse(edgeExcentricity),     // excentricity at the edge of the disk
                                    //double.Parse(sigma),                // Sigma
                                    //int.Parse(coreOrbitalVelocity),     // orbital velocity at the edge of the core
                                    //int.Parse(edgeOrbitalVelocity),     // orbital velocity at the edge of the disk
                                    int.Parse(starCount),               // total number of stars
                                    int.Parse(dustCount)                // amount of dust
                                );

                        SingleTimeStep(100); //100 years
                        for (int i = 0; i < m_pH2.Count; i++)
                        {
                            GameObject h2 = (GameObject)Instantiate(H2Prefab, new Vector3(m_pH2[i].m_pos.x, 0, m_pH2[i].m_pos.y), new Quaternion(0, 0, 0, 0));
                            h2.transform.SetParent(gameObject.transform, false);
                            h2Stars.Add(h2);
                        }

                        for (int i = 0; i < m_pStars.Count; i++)
                        {
                            GameObject node = (GameObject)Instantiate(nodePrefab, new Vector3(m_pStars[i].m_pos.x, 0, m_pStars[i].m_pos.y), new Quaternion(0, 0, 0, 0));
                            node.transform.SetParent(gameObject.transform, false);
                            nodes.Add(node);
                        }
                        for (int i = 0; i < m_pDust.Count; i++)
                        {
                            GameObject dustObject = (GameObject)Instantiate(dustPrefab, new Vector3(m_pDust[i].m_pos.x, 0, m_pDust[i].m_pos.y), new Quaternion(0, 0, 0, 0));
                            dustObject.transform.SetParent(gameObject.transform, false);
                            dust.Add(dustObject);
                        }
                    }
                }
            }
        }
        if (commands.Count > 0)
        {
            commands.RemoveAll(AllCommands);
        }

        SingleTimeStep(100000); //100 years

        for (int i = 0; i < nodes.Count; i++)
        {
            nodes[i].transform.position = new Vector3(m_pStars[i].m_pos.x, 0, m_pStars[i].m_pos.y);
        }
        for (int i = 0; i < dust.Count; i++)
        {
            dust[i].transform.position = new Vector3(m_pDust[i].m_pos.x, 0, m_pDust[i].m_pos.y);
        }
        for (int i = 0; i < h2Stars.Count; i++)
        {
            h2Stars[i].transform.position = new Vector3(m_pH2[i].m_pos.x, 0, m_pH2[i].m_pos.y);
        }

    }
    private static bool AllCommands(String s)
    {
        return true;
    }


    // Generate the galaxy

    void NewGalaxy( double rad,
                double radCore,
                double deltaAng,
                double ex1,
                double ex2,
//                double sigma,
 //               double velInner,
  //              double velOuter,
                int numStars,
                int numDust)
    {
        m_elEx1 = ex1;
        m_elEx2 = ex2;
        //m_velInner = velInner;
        //m_velOuter = velOuter;
        m_angleOffset = deltaAng;
        m_radCore = radCore;
        m_radGalaxy = rad;
        m_radFarField = m_radGalaxy * 2;  // there is no science behind this threshold it just should look nice
        //m_sigma = sigma;
        m_numStars = numStars;
        m_numDust = numDust;
        m_time = 0;

        //for (int i = 0; i < 100; ++i)
        //    m_numberByRad[i] = 0;

//        InitStars(m_sigma);
        InitStars();
    }

//    void InitStars(double sigma)
    void InitStars()
    {
        //m_pDust = new Star[m_numDust];

        //m_pStars = new Star[m_numStars];

        //m_pH2 = new Star[m_numH2 * 2];
        m_pDust.Clear();
        m_pStars.Clear();
        m_pH2.Clear();

        Star star, dust, H2;
        
        star = new Star();
        m_pStars.Add(star);
        // The first three stars can be used for aligning the
        // camera with the galaxy rotation.

        // First star ist the black hole at the centre
        m_pStars[0].m_a = 0;
        m_pStars[0].m_b = 0;
        m_pStars[0].m_angle = 0;
        m_pStars[0].m_theta = 0;
        m_pStars[0].m_velTheta = 0;
        m_pStars[0].m_center = new Vector2(0, 0);
        m_pStars[0].m_velTheta = 0; //= GetOrbitalVelocity((m_pStars[0].m_a + m_pStars[0].m_b) / 2.0);
        m_pStars[0].m_temp = 6000;

        // second star is at the edge of the core area
        star = new Star();
        m_pStars.Add(star);

        m_pStars[1].m_a = m_radCore;
        m_pStars[1].m_b = m_radCore * GetExcentricity(m_radCore);
        m_pStars[1].m_angle = GetAngularOffset(m_radCore);
        m_pStars[1].m_theta = 0;
        m_pStars[1].m_center = new Vector2(0, 0);
        m_pStars[1].m_velTheta = GetOrbitalVelocity((m_pStars[1].m_a + m_pStars[1].m_b) / 2.0);
        m_pStars[1].m_temp = 6000;

        star = new Star();
        m_pStars.Add(star);

        // third star is at the edge of the disk
        m_pStars[2].m_a = m_radGalaxy;
        m_pStars[2].m_b = m_radGalaxy * GetExcentricity(m_radGalaxy);
        m_pStars[2].m_angle = GetAngularOffset(m_radGalaxy);
        m_pStars[2].m_theta = 0;
        m_pStars[2].m_center = new Vector2(0, 0);
        m_pStars[2].m_velTheta = GetOrbitalVelocity((m_pStars[2].m_a + m_pStars[2].m_b) / 2.0);
        m_pStars[2].m_temp = 6000;

        // cell width of the histogramm
        //double dh = (double)m_radFarField / 100.0;

        // Initialize the stars
        CumulativeDistributionFunction cdf = new CumulativeDistributionFunction();
        cdf.SetupRealistic(1.0,             // Maximalintensität
                           0.02,            // k (bulge)
                           m_radGalaxy / 3.0, // disc skalenlänge
                           m_radCore,       // bulge radius
                           0,               // start der intensitätskurve
                           m_radFarField,   // ende der intensitätskurve
                           1000);           // Anzahl der stützstellen
        for (int i = 3; i < m_numStars; ++i)
        {
            // random value between -1 and 1
            //double sum = -6;
            //for (int j = 0; j < 12; ++j)
            //{
            //    sum += UnityEngine.Random.Range(0, RAND_MAX) / (double)RAND_MAX;
            //}
            //double rad = Math.Abs(sum) * m_radGalaxy;
            
            double rad = cdf.ValFromProb((double)UnityEngine.Random.Range(0, RAND_MAX) / (double)RAND_MAX);
            star = new Star();

            star.m_a = rad;
            star.m_b = rad * GetExcentricity(rad);
            star.m_angle = GetAngularOffset(rad);
            star.m_theta = 360.0 * ((double)UnityEngine.Random.Range(0, RAND_MAX) / (double) RAND_MAX);
            star.m_velTheta = GetOrbitalVelocity(rad);
            star.m_center = new Vector2(0, 0);
            star.m_temp = 6000 + (4000 * ((double)UnityEngine.Random.Range(0, RAND_MAX) / (double) RAND_MAX)) - 2000;
            star.m_mag = 0.1 + 0.2 * (double)UnityEngine.Random.Range(0, RAND_MAX) / (double)RAND_MAX;

            //int idx = (int)Math.Min(1.0 / dh * (star.m_a + star.m_b) / 2.0, 99.0);
            m_pStars.Add(star);
            //m_numberByRad[idx]++;
        }

        // Initialise Dust
        double x, y;
        for (int i = 0; i < m_numDust; ++i)
        {
            dust = new Star();
            if (i % 4 == 0)
            {
                rad = cdf.ValFromProb((double)UnityEngine.Random.Range(0, RAND_MAX) / (double)RAND_MAX);
            }
            else
            {
                x = 2 * m_radGalaxy * ((double)UnityEngine.Random.Range(0, RAND_MAX) / (double)RAND_MAX) - m_radGalaxy;
                y = 2 * m_radGalaxy * ((double)UnityEngine.Random.Range(0, RAND_MAX) / (double)RAND_MAX) - m_radGalaxy;
                rad = Math.Sqrt(x * x + y * y);
            }

            dust.m_a = rad;
            dust.m_b = rad * GetExcentricity(rad);
            dust.m_angle = GetAngularOffset(rad);
            dust.m_theta = 360.0 * ((double)UnityEngine.Random.Range(0, RAND_MAX) / (double)RAND_MAX);
            dust.m_velTheta = GetOrbitalVelocity((dust.m_a + dust.m_b) / 2.0);
            dust.m_center = new Vector2(0, 0);

            // I want the outer parts to appear blue, the inner parts yellow. I'm imposing
            // the following temperature distribution (no science here it just looks right)
            dust.m_temp = 5000 + rad / 4.5;

            dust.m_mag = 0.015 + 0.01 * (double)UnityEngine.Random.Range(0, RAND_MAX) / (double)RAND_MAX;
            
            m_pDust.Add(dust);
        }

        // Initialise H2

        for (int i = 0; i < m_numH2*2; ++i)
        {
            H2 = new Star();
            m_pH2.Add(H2);
        }
        for (int i = 0; i < m_numH2; ++i)
        {

            x = 2 * (m_radGalaxy) * ((double)UnityEngine.Random.Range(0, RAND_MAX) / (double)RAND_MAX) - (m_radGalaxy);
            y = 2 * (m_radGalaxy) * ((double)UnityEngine.Random.Range(0, RAND_MAX) / (double)RAND_MAX) - (m_radGalaxy);
            rad = Math.Sqrt(x * x + y * y);

            int k1 = 2 * i;
            m_pH2[k1].m_a = rad;
            m_pH2[k1].m_b = rad * GetExcentricity(rad);
            m_pH2[k1].m_angle = GetAngularOffset(rad);
            m_pH2[k1].m_theta = 360.0 * ((double)UnityEngine.Random.Range(0, RAND_MAX) / (double)RAND_MAX);
            m_pH2[k1].m_velTheta = GetOrbitalVelocity((m_pH2[k1].m_a + m_pH2[k1].m_b) / 2.0);
            m_pH2[k1].m_center = new Vector2(0, 0);
            m_pH2[k1].m_temp = 6000 + (6000 * ((double)UnityEngine.Random.Range(0, RAND_MAX) / (double)RAND_MAX)) - 3000;
            m_pH2[k1].m_mag = 0.1 + 0.05 * (double)UnityEngine.Random.Range(0, RAND_MAX) / (double)RAND_MAX;

            int k2 = 2 * i + 1;
            m_pH2[k2].m_a = rad + 1000;
            m_pH2[k2].m_b = rad * GetExcentricity(rad);
            m_pH2[k2].m_angle = GetAngularOffset(rad);
            m_pH2[k2].m_theta = m_pH2[k1].m_theta;
            m_pH2[k2].m_velTheta = m_pH2[k1].m_velTheta;
            m_pH2[k2].m_center = m_pH2[k1].m_center;
            m_pH2[k2].m_temp = m_pH2[k1].m_temp;
            m_pH2[k2].m_mag = m_pH2[k1].m_mag;

        }
    }



    /* Returns the orbital velocity in degrees per year.
       param rad Radius in parsec
    */
    double GetOrbitalVelocity(double rad) 
    {
        double vel_kms;  // velocity in kilometer per seconds

        //  with dark matter
        vel_kms = VelocityCurve.v(rad);

       // without dark matter:
       // vel_kms = VelocityCurve.vd(rad);

        // Calculate velocity in degree per year
        double u = 2 * Math.PI * rad * PC_TO_KM;    
        double time = u / (vel_kms * SEC_PER_YEAR);  

        return 360.0 / time;                                  
    }

    double GetExcentricity(double r) 
    {
        if (r<m_radCore)
        {
            // Core region of the galaxy. Innermost part is round
            // excentricity increasing linear to the border of the core.
            return 1 + (r / m_radCore) * (m_elEx1-1);
        }
        else if (r>m_radCore && r<=m_radGalaxy)
        {
            return m_elEx1 + (r-m_radCore) / (m_radGalaxy-m_radCore) * (m_elEx2-m_elEx1);
        }
        else if (r>m_radGalaxy && r<m_radFarField)
        {
            // excentricity is slowly reduced to 1.
            return m_elEx2 + (r - m_radGalaxy) / (m_radFarField - m_radGalaxy) * (1-m_elEx2);
        }
        else
            return 1;
    }

    double GetAngularOffset(double rad) 
    {
        return rad* m_angleOffset;
    }

    void SingleTimeStep(double time)
    {
        //m_timeStep = time;
        m_time += time;

        Vector2 posOld;
        for (int i = 0; i < m_numStars; ++i)
        {
            m_pStars[i].m_theta += (m_pStars[i].m_velTheta * time);
            posOld = m_pStars[i].m_pos;
            m_pStars[i].CalcXY();

            Vector2 b = new Vector2(m_pStars[i].m_pos.x - posOld.x,
                            m_pStars[i].m_pos.y - posOld.y);
            m_pStars[i].m_vel = b;
        }

        for (int i = 0; i < m_numDust; ++i)
        {
            m_pDust[i].m_theta += (m_pDust[i].m_velTheta * time);
            posOld = m_pDust[i].m_pos;
            m_pDust[i].CalcXY();
        }
        if (m_numStars > 0)
        {
            for (int i = 0; i < m_numH2 * 2; ++i)
            {
                m_pH2[i].m_theta += (m_pH2[i].m_velTheta * time);
                posOld = m_pDust[i].m_pos;
                m_pH2[i].CalcXY();
            }
        }
    }
}

public class Star
{
    private double DEG_TO_RAD = Math.PI / 180.0;

    public Vector2 CalcXY()
    {
        double beta = -m_angle,
        alpha = m_theta * DEG_TO_RAD;

        // temporaries to save cpu time
        double cosalpha = Math.Cos(alpha),
        sinalpha = Math.Sin(alpha),
        cosbeta = Math.Cos(beta),
        sinbeta = Math.Sin(beta);

        m_pos = new Vector2((float)(m_center.x + (m_a * cosalpha * cosbeta - m_b * sinalpha * sinbeta))/100, 
                            (float)(m_center.y + (m_a * cosalpha * sinbeta + m_b * sinalpha * cosbeta))/100);
        return m_pos;
    }

    public double m_theta;    // position auf der ellipse
    public double m_velTheta; // angular velocity
    public double m_angle;    // Schräglage der Ellipse
    public double m_a;        // kleine halbachse
    public double m_b;        // große halbachse
    public double m_temp;     // star temperature
    public double m_mag;      // brigtness;
    public Vector2 m_center;   // center of the elliptical orbit
    public Vector2 m_vel;      // Current velocity (calculated)
    public Vector2 m_pos;      // current position in kartesion koordinates
}

// Realistically looking velocity curves for the Wikipedia models.
public class VelocityCurve
{
    static public double MS(double r)
    {
        double d = 2000;  // Dicke der Scheibe
        double rho_so = 1;  // Dichte im Mittelpunkt
        double rH = 2000; // Radius auf dem die Dichte um die Hälfte gefallen ist
        return rho_so * Math.Exp(-r / rH) * (r * r) * Math.PI * d;
    }

    static public double MH(double r)
    {
        double rho_h0 = 0.15; // Dichte des Halos im Zentrum
        double rC = 2500;     // typische skalenlänge im Halo
        return rho_h0 * 1 / (1 + Math.Pow(r / rC, 2)) * (4 * Math.PI * Math.Pow(r, 3) / 3);
    }

    // Velocity curve with dark matter
    static public double v(double r)
    {
        double MZ = 100;
        double G = 6.672e-11;
        return 20000 * Math.Sqrt(G * (MH(r) + MS(r) + MZ) / r);
    }

    // velocity curve without dark matter
    static public double vd(double r)
    {
        double MZ = 100;
        double G = 6.672e-11;
        return 20000 * Math.Sqrt(G * (MS(r) + MZ) / r);
    }
}

class CumulativeDistributionFunction
{
    public void SetupRealistic(double I0, double k, double a, double RBulge, double min, double max, int nSteps)
    {
        m_fMin = min;
        m_fMax = max;
        m_nSteps = nSteps;

        m_I0 = I0;
        m_k = k;
        m_a = a;
        m_RBulge = RBulge;

        // build the distribution function
        BuildCDF(m_nSteps);
    }

    public void BuildCDF(int nSteps)
    {
        double h = (m_fMax - m_fMin) / nSteps;
        double x = 0, y = 0;

        m_vX1.Clear();
        m_vY1.Clear();
        m_vX2.Clear();
        m_vY2.Clear();
        m_vM1.Clear();
        m_vM2.Clear();

        // Simpson rule for integration of the distribution function
        m_vY1.Add(0.0);
        m_vX1.Add(0.0);
        for (int i = 0; i < nSteps; i += 2)
        {
            x = (i + 2) * h;
            y += h / 3 * (Intensity(m_fMin + i * h) + 4 * Intensity(m_fMin + (i + 1) * h) + Intensity(m_fMin + (i + 2) * h));

            m_vM1.Add((y - m_vY1[m_vY1.Count - 1]) / (2 * h));
            m_vX1.Add(x);
            m_vY1.Add(y);

            //    printf("%2.2f, %2.2f, %2.2f\n", m_fMin + (i+2) * h, v, h);
        }
        m_vM1.Add(0.0);

        // all arrays must have the same length
//        if (m_vM1.Count != m_vX1.Count || m_vM1.Count != m_vY1.Count)
  //          throw std::runtime_error("CumulativeDistributionFunction::BuildCDF: array size mismatch (1)!");

        // normieren
        for (int i = 0; i < m_vY1.Count; ++i)
        {
            m_vY1[i] /= m_vY1[m_vY1.Count - 1];
            m_vM1[i] /= m_vY1[m_vY1.Count - 1];
        }

        //
        m_vX2.Add(0.0);
        m_vY2.Add(0.0);

        double p = 0;
        h = 1.0 / nSteps;
        for (int i = 1, k = 0; i < nSteps; ++i)
        {
            p = (double)i * h;

            for (; m_vY1[k + 1] <= p; ++k)
            { }


            y = m_vX1[k] + (p - m_vY1[k]) / m_vM1[k];

           // printf("%2.4f, %2.4f, k=%d, %2.4f, %2.4f\n", p, y, k, m_vY1[k], m_vM1[k]);

            m_vM2.Add((y - m_vY2[m_vY2.Count - 1]) / h);
            m_vX2.Add(p);
            m_vY2.Add(y);
        }
        m_vM2.Add(0.0);

        // all arrays must have the same length
//        if (m_vM2.Count != m_vX2.Count || m_vM2.Count != m_vY2.Count)
//            throw std::runtime_error("CumulativeDistributionFunction::BuildCDF: array size mismatch (1)!");

    }

    public double ProbFromVal(double fVal)
    {
        double h = 2 * ((m_fMax - m_fMin) / m_nSteps);
        int i = (int)((fVal - m_fMin) / h);
        double remainder = fVal - i * h;

        //  printf("fVal=%2.2f; h=%2.2f; i=%d; m_vVal[i]=%2.2f; m_vAsc[i]=%2.2f;\n", fVal, h, i, m_vVal[i], m_vAsc[i]);

        return (m_vY1[i] + m_vM1[i] * remainder) /* / m_vVal.back()*/;
    }

    //-------------------------------------------------------------------------------------------------
    public double ValFromProb(double fVal)
    {
        double h = 1.0 / m_vY2.Count;

        int i = (int)(fVal / h);
        double remainder = fVal - i * h;

        return (m_vY2[i] + m_vM2[i] * remainder) /* / m_vVal.back()*/;
    }

    //-------------------------------------------------------------------------------------------------
    public double IntensityBulge(double R, double I0, double k)
    {
        return I0 * Math.Exp(-k * Math.Pow(R, 0.25));
    }

    //-------------------------------------------------------------------------------------------------
    public double IntensityDisc(double R, double I0, double a)
    {
        return I0 * Math.Exp(-R / a);
    }

    //-------------------------------------------------------------------------------------------------
    public double Intensity(double x)
    {
        return (x < m_RBulge) ? IntensityBulge(x, m_I0, m_k) : IntensityDisc(x - m_RBulge, IntensityBulge(m_RBulge, m_I0, m_k), m_a);
    }

    double m_fMin;
    double m_fMax;
    double m_fWidth;
    int m_nSteps;

    // parameters for realistic star distribution
    double m_I0;
    double m_k;
    double m_a;
    double m_RBulge;

    private List<double> m_vM1 = new List<double>();
    private List<double> m_vY1 = new List<double>();
    private List<double> m_vX1 = new List<double>();

    private List<double> m_vM2 = new List<double>();
    private List<double> m_vY2 = new List<double>();
    private List<double> m_vX2 = new List<double>();
}