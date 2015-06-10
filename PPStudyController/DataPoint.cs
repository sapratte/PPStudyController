using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PPStudyController
{
    class DataPoint
    {
        public double locationX = 1;
        public double locationY = 0;
        public double locationZ = 1;

        public double dropRange = 2;

        public List<string> data = new List<string>();

        public string observerType = "rectangle";
        public double observeWidth = 1;
        public double observeHeight = 1;
        public double observerDistance = 2;

        public string subscriberType = "device";
        public int ID = 57;

    }
}
