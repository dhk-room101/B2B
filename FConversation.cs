using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TESVSnip
{
    public class FConversation
    {
        //TODO add other conv attributes
        public int ResRefID = 0;
        public string ResRefName = string.Empty;
        public List<int> StartList = new List<int>();
        public List<FConvNode> NPCLineList = new List<FConvNode>();
        public List<FConvNode> PlayerLineList = new List<FConvNode>();
    };
}
