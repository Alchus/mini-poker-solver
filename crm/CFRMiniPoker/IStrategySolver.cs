using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CFRMiniPoker
{
    internal interface IStrategySolver<TAction>
    {
        void Train(int iterations);

        void Save(string filePath);
        bool TryLoad(string filePath);

        
        public IPlayer<TAction> FreezeStrategy();


    }
}
