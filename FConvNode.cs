using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TESVSnip
{
    // node in conversation, not related to what is in progress
    public class FConvNode
    {
        //DHK         
        //Keeps track of position in the Conversation branch
        public int lineIndex = 0;

        //DAO
        public int StringID = 0;
        public int LanguageID = 0;
        public int ConditionScriptURI = 0;
        public int ConditionParameter = 0;
        public string ConditionParameterText = "";
        public int ConditionPlotURI = 0;
        public int ConditionPlotFlag = -1;
        public bool ConditionResult = false;
        public int ActionScriptURI = 0;
        public int ActionParameter = 0;
        public string ActionParameterText = "";
        public int ActionPlotURI = 0;
        public int ActionPlotFlag = -1;
        public bool ActionResult = false;
        public string text = "";
        public bool TextRequiresReTranslation = false;
        public bool TextRequiresReRecording = false;
        public string Speaker = "";
        public string PreviousSpeaker = "";
        public string Listener = "";
        public int icon = 0;
        public string Comment = "";
        public int FastPath = 0;
        public string SlideShowTexture = "";
        public string VoiceOverTag = "";
        public string VoiceOverComment = "";
        public string EditorComment = "";
        public int LineVisibility = 0;
        public bool Ambient = false;
        public bool SkipLine = false;
        public int StageURI = 0;
        public string StageTag = "";
        public bool StageAtCurrentLocation = false;
        public string CameraTag = "";
        public bool CameraLocked = false;
        public string SecondaryCameratag = "";
        public float SecondaryCameraDelay = 0;
        public int Emotion = 0;
        public int CustomCutsceneURI = 0;
        public string SpeakerAnimation = "";
        public bool RevertAnimation = false;
        public bool LockAnimations = false;
        public bool PlaySoundEvents = false;
        public int RoboBradSeed = 0;
        public bool RoboBradSeedOverride = false;
        public bool RoboBradLocked = false;
        public int PreviewAreaURI = 0;
        public bool PreviewStageUseFirstMatch = false;
        public bool UseAnimationDuration = false;
        public bool NoVOInGame = false;
        public bool Narration = false;
        public bool PreCacheVO = false;
        public List<FConvTransition> TransitionList = new List<FConvTransition>();
        //TODO FConvNode Lists
        // 	CinematicsInfoList 
        //List<FConvAnimationListList> AnimationListList;
        // 	CustomCutsceneParameterList 
        // 	PreviewTagMappingList 
    };
}
