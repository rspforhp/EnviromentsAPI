using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using APIPlugin;
using DiskCardGame;
using BepInEx;
using BepInEx.Logging;
using EasyFeedback.APIs;
using GBC;
using HarmonyLib;
using Pixelplacement;
using UnityEngine;
using Logger = UnityEngine.Logger;

namespace enviroments
{



    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
	    


        private const string PluginGuid = "kopie.inscryption.enviroments";
        private const string PluginName = "enviroments";
        private const string PluginVersion = "1.0.0";
        internal static ManualLogSource Log;
        public static string staticpath;
        public static List<CardInfo> enviromentcards=new List<CardInfo>();
        public static List<Type> enviromentcardstypes=new List<Type>();
        public static List<string> enviromentcardsrulebookdescription=new List<string>();
        public bool mousepressed2;





        [HarmonyPatch(typeof(PlayableCard), "OnPlayed")]
        public class patchtheplayofcard
        {
	        static void Postfix(ref PlayableCard __instance)
	        {
		        foreach (var VARIABLE in __instance.slot.gameObject.GetComponents(typeof(baseslottriggerreciever)))
		        {
			        
			        object[] parametersArray =  new object[] {  __instance  };

			        var methodinfo =VARIABLE.GetType().GetMethod("OnCardPlayedHere");
			        methodinfo.Invoke(VARIABLE, parametersArray);
			        
		        }

	        }
        }


        [HarmonyPatch(typeof(TurnManager), "DoUpkeepPhase")]
        public class patchtheupkeep
        {
	        static void Prefix(out TurnManager __state, ref TurnManager __instance)
	        {
		        __state = __instance;
	        }

	        public static IEnumerator Postfix(IEnumerator enumerator, TurnManager __state, bool playerUpkeep)
	        {
		        Singleton<PlayerHand>.Instance.CardsDrawnThisTurn = 0;
		        Singleton<GlobalTriggerHandler>.Instance.AbilitiesTriggeredThisTurn.Clear();
		        Singleton<BoardManager>.Instance.CardsOnBoard.ForEach(delegate(PlayableCard x)
		        {
			        x.OnUpkeep(playerUpkeep);
		        });
		        foreach (var slots in GameObject.FindObjectsOfType<CardSlot>())
		        {
			        foreach (var VARIABLE in slots.gameObject.GetComponents(typeof(baseslottriggerreciever)))
			        {
				        object[] parametersArray =  new object[] {  playerUpkeep  };
				        var methodinfo =VARIABLE.GetType().GetMethod("OnUpkeep");
				        yield return methodinfo.Invoke(VARIABLE, parametersArray);
                    
			        }
		        }

		        if (playerUpkeep)
		        {
			        Singleton<BoardManager>.Instance.PlayerCardsPlayedThisRound.Clear();
		        }
		        else
		        {
			        __state.opponent.OnUpkeep();
		        }
		        yield return Singleton<GlobalTriggerHandler>.Instance.TriggerCardsOnBoard(Trigger.Upkeep, true, new object[]
		        {
			        playerUpkeep
		        });
		        if (__state.SpecialSequencer != null)
		        {
			        if (playerUpkeep)
			        {
				        yield return __state.SpecialSequencer.PlayerUpkeep();
			        }
			        else
			        {
				        yield return __state.SpecialSequencer.OpponentUpkeep();
			        }
		        }
		        yield break;   
	        }
        }

        
        [HarmonyPatch(typeof(BoardManager), "ChooseSacrificesForCard")]
        public class patchthesacrifice
        {
	        static void Prefix(out BoardManager __state, ref BoardManager __instance)
	        {
		        __state = __instance;
	        }

	        public static IEnumerator Postfix(IEnumerator enumerator, List<CardSlot> validSlots, PlayableCard card, BoardManager __state)
	        {
		        			Singleton<ViewManager>.Instance.Controller.LockState = ViewLockState.Unlocked;
			Singleton<ViewManager>.Instance.SwitchToView(__state.BoardView, false, false);
			Singleton<InteractionCursor>.Instance.ForceCursorType(CursorType.Sacrifice);
			__state.cancelledPlacementWithInput = false;
			__state.currentValidSlots = validSlots;
			__state.currentSacrificeDemandingCard = card;
			__state.CancelledSacrifice = false;
			__state.LastSacrificesInfo.Clear();
			__state.SetQueueSlotsEnabled(false);
			foreach (CardSlot cardSlot in __state.AllSlots)
			{
				if (!cardSlot.IsPlayerSlot || cardSlot.Card == null)
				{
					cardSlot.SetEnabled(false);
					cardSlot.ShowState(HighlightedInteractable.State.NonInteractable, false, 0.15f);
				}
				if (cardSlot.IsPlayerSlot && cardSlot.Card != null && cardSlot.Card.CanBeSacrificed)
				{
					cardSlot.Card.Anim.SetShaking(true);
				}
			}
			yield return __state.SetSacrificeMarkersShown(card.Info.BloodCost);
			while (__state.GetValueOfSacrifices(__state.currentSacrifices) < card.Info.BloodCost && !__state.cancelledPlacementWithInput)
			{
				__state.SetSacrificeMarkersValue(__state.currentSacrifices.Count);
				yield return new WaitForEndOfFrame();
			}
			foreach (CardSlot cardSlot2 in __state.AllSlots)
			{
				cardSlot2.SetEnabled(false);
				if (cardSlot2.IsPlayerSlot && cardSlot2.Card != null)
				{
					cardSlot2.Card.Anim.SetShaking(false);
				}
			}
			foreach (CardSlot cardSlot3 in __state.currentSacrifices)
			{
				__state.LastSacrificesInfo.Add(cardSlot3.Card.Info);
			}
			bool flag = !__state.SacrificesCreateRoomForCard(card, __state.currentSacrifices);
			if (__state.cancelledPlacementWithInput || flag)
			{
				__state.HideSacrificeMarkers();
				if (flag)
				{
					yield return new WaitForSeconds(0.25f);
				}
				foreach (CardSlot cardSlot4 in __state.GetSlots(true))
				{
					if (cardSlot4.Card != null)
					{
						cardSlot4.Card.Anim.SetSacrificeHoverMarkerShown(false);
						if (__state.currentSacrifices.Contains(cardSlot4))
						{
							cardSlot4.Card.Anim.SetMarkedForSacrifice(false);
						}
					}
				}
				Singleton<ViewManager>.Instance.SwitchToView(__state.defaultView, false, false);
				Singleton<InteractionCursor>.Instance.ClearForcedCursorType();
				__state.CancelledSacrifice = true;
			}
			else
			{
				__state.SetSacrificeMarkersValue(__state.GetValueOfSacrifices(__state.currentSacrifices));
				yield return new WaitForSeconds(0.2f);
				__state.HideSacrificeMarkers();
				foreach (CardSlot cardSlot5 in __state.currentSacrifices)
				{
					if (cardSlot5.Card != null && !cardSlot5.Card.Dead)
					{
						foreach (var VARIABLE in cardSlot5.gameObject.GetComponents(typeof(baseslottriggerreciever)))
						{
							object[] parametersArray =  new object[] {  cardSlot5.Card, card  };
							var methodinfo =VARIABLE.GetType().GetMethod("OnSacrifice");
							yield return methodinfo.Invoke(VARIABLE, parametersArray);
                    
						}
						yield return __state.StartCoroutine(cardSlot5.Card.Sacrifice());
						Singleton<ViewManager>.Instance.SwitchToView(__state.BoardView, false, false);
					}
				}
				List<CardSlot>.Enumerator enumerator2 = default(List<CardSlot>.Enumerator);
			}
			__state.SetQueueSlotsEnabled(true);
			foreach (CardSlot cardSlot6 in __state.AllSlots)
			{
				cardSlot6.SetEnabled(true);
				cardSlot6.ShowState(HighlightedInteractable.State.Interactable, false, 0.15f);
			}
			__state.currentSacrificeDemandingCard = null;
			__state.currentSacrifices.Clear();
			yield break;
			yield break;
	        }
        }

        
        
        
        
        [HarmonyPatch(typeof(PlayableCard), "Die")]
        public class patchthedeath
        {
	        static void Prefix(out PlayableCard __state, ref PlayableCard __instance)
	        {
		        __state = __instance;
	        }
	        
	        public static IEnumerator Postfix(IEnumerator enumerator, PlayableCard __state, bool wasSacrifice, PlayableCard killer = null, bool playSound = true)
	        {
		        foreach (var VARIABLE in __state.slot.gameObject.GetComponents(typeof(baseslottriggerreciever)))
		        {
			        
			        object[] parametersArray =  new object[] {   __state  };

			        var methodinfo =VARIABLE.GetType().GetMethod("OnCardDieHere");
				        yield return methodinfo.Invoke(VARIABLE, parametersArray);
			        
		        }

		        if (!__state.Dead)
		        {
			        __state.Dead = true;
			        CardSlot slotBeforeDeath = __state.Slot;
			        if (__state.TriggerHandler.RespondsToTrigger(Trigger.PreDeathAnimation, new object[]
			        {
				        wasSacrifice
			        }))
			        {
				        yield return __state.TriggerHandler.OnTrigger(Trigger.PreDeathAnimation, new object[]
				        {
					        wasSacrifice
				        });
			        }
			        yield return Singleton<GlobalTriggerHandler>.Instance.TriggerCardsOnBoard(Trigger.OtherCardPreDeath, false, new object[]
			        {
				        slotBeforeDeath,
				        !wasSacrifice,
				        killer
			        });
			        __state.Anim.SetShielded(false);
			        yield return __state.Anim.ClearLatchAbility();
			        if (__state.HasAbility(Ability.PermaDeath))
			        {
				        __state.Anim.PlayPermaDeathAnimation(playSound && !wasSacrifice);
				        yield return new WaitForSeconds(1.25f);
			        }
			        else if (__state.InOpponentQueue)
			        {
				        __state.Anim.PlayQueuedDeathAnimation(playSound && !wasSacrifice);
			        }
			        else
			        {
				        __state.Anim.PlayDeathAnimation(playSound && !wasSacrifice);
			        }
			        if (!__state.HasAbility(Ability.QuadrupleBones) && slotBeforeDeath.IsPlayerSlot)
			        {
				        yield return Singleton<ResourcesManager>.Instance.AddBones(1, slotBeforeDeath);
			        }
			        if (__state.TriggerHandler.RespondsToTrigger(Trigger.Die, new object[]
			        {
				        wasSacrifice,
				        killer
			        }))
			        {
				        yield return __state.TriggerHandler.OnTrigger(Trigger.Die, new object[]
				        {
					        wasSacrifice,
					        killer
				        });
			        }
			        yield return Singleton<GlobalTriggerHandler>.Instance.TriggerAll(Trigger.OtherCardDie, false, new object[]
			        {
				        __state,
				        slotBeforeDeath,
				        !wasSacrifice,
				        killer
			        });
			        __state.UnassignFromSlot();
			        __state.StartCoroutine(__state.DestroyWhenStackIsClear());
			        slotBeforeDeath = null;
		        }
		        yield break;
	        }
        }








        [HarmonyPatch(typeof(BoardManager), "ClearBoard")]
        public class patchtheclearboard
        {


	        static void Prefix(out BoardManager __state, ref BoardManager __instance)
	        {
		        __state = __instance;
	        }

	        public static IEnumerator Postfix(IEnumerator enumerator, BoardManager __state)
	        {
		        while (enumerator.MoveNext())
		        {
			        enumerator.MoveNext();
		        }

		        foreach (var gameObject in GameObject.FindObjectsOfType<CardSlot>())
		        {
			        foreach (var component in gameObject.GetComponents(typeof(baseslottriggerreciever)))
			        {
				        Destroy(component);
			        }
		        }
		        yield break;
	        }
        }



        [HarmonyPatch(typeof(BoardManager), "AssignCardToSlot")]
        public class patchtheassigningtoslot
        {
	        public static IEnumerator moveoutcoroutine(CardSlot slot2, PlayableCard card, CardSlot slot)
	        {
		        if (slot2 != null)
		        {
			        yield return new WaitForSeconds(0.65f);
			        foreach (var VARIABLE in slot2.gameObject.GetComponents(typeof(baseslottriggerreciever)))
			        {
			        
				        object[] parametersArray =  new object[] {   card, slot  };

				        var methodinfo =VARIABLE.GetType().GetMethod("OnCardMoveOut");
				        yield return methodinfo.Invoke(VARIABLE, parametersArray);
			        
			        }

		        }
		        yield break;
	        }
	        
	        static void Prefix(out BoardManager __state, ref BoardManager __instance)
	        {
		        __state = __instance;
	        }

	        public static IEnumerator Postfix(IEnumerator enumerator, BoardManager __state, PlayableCard card,
		        CardSlot slot, float transitionDuration = 0.1f, Action tweenCompleteCallback = null,
		        bool resolveTriggers = true)
	        {

		        

		        if (card != null)
		        {
			        
			        CardSlot slot2 = card.Slot;

			        if (card.Slot != null)
			        {
				        card.Slot.Card = null;
			        }
			        if (slot.Card != null)
			        {
				        slot.Card.Slot = null;
			        }
			        card.SetEnabled(false);
			        slot.Card = card;
			        card.Slot = slot;
			        card.RenderCard();
			        if (!slot.IsPlayerSlot)
			        {
				        card.SetIsOpponentCard(true);
			        }
			        card.transform.parent = slot.transform;
			        card.Anim.PlayRiffleSound();
			        Tween.LocalPosition(card.transform, Vector3.up * (__state.SlotHeightOffset + card.SlotHeightOffset), transitionDuration, 0.05f, Tween.EaseOut, Tween.LoopType.None, null, delegate()
			        {
				        Action tweenCompleteCallback2 = tweenCompleteCallback;
				        if (tweenCompleteCallback2 != null)
				        {
					        tweenCompleteCallback2();
				        }
				        card.Anim.PlayRiffleSound();
			        }, true);
			        Tween.Rotation(card.transform, slot.transform.GetChild(0).rotation, transitionDuration, 0f, Tween.EaseOut, Tween.LoopType.None, null, null, true);
			        if (resolveTriggers && slot2 != card.Slot)
			        {
				        yield return Singleton<GlobalTriggerHandler>.Instance.TriggerCardsOnBoard(Trigger.OtherCardAssignedToSlot, false, new object[]
				        {
					        card
				        });
			        }
			        foreach (var VARIABLE in slot.gameObject.GetComponents(typeof(baseslottriggerreciever)))
			        {
				        
				        object[] parametersArray =  new object[] {   card, slot2  };

				        var methodinfo =VARIABLE.GetType().GetMethod("OnCardMoveIn");
				        yield return methodinfo.Invoke(VARIABLE, parametersArray);
                    
			        }


			        card.StartCoroutine(moveoutcoroutine(slot2, card, slot));
			        yield break;
		        }
		        yield break;
	        }
        }


        [HarmonyPatch(typeof(HighlightedInteractable), "Awake")]
        public class addthetriggerrecievertoaslot
        {
	        static void Prefix(ref HighlightedInteractable __instance)
	        {
		        if (__instance.GetComponent<CardSlot>())
		        {
			        __instance.gameObject.AddComponent<baseslottriggerreciever>();
		        }
		        
	        }
        }

        public class baseslottriggerreciever : MonoBehaviour
        {
	        public virtual bool RespondsToCardPlayedHere(PlayableCard card)
	        {
		        return false;
	        }


	        public virtual void OnCardPlayedHere(PlayableCard card)
	        {
		        return;
	        }
	        
	        public virtual bool RespondsToCardMoveIn(PlayableCard card, CardSlot fromslot)
	        {
		        return false;
	        }


	        public virtual IEnumerator OnCardMoveIn(PlayableCard card, CardSlot fromslot)
	        {
		        yield break;
	        }
	        
	        public virtual bool RespondsToUpkeep(bool playerUpkeep)
	        {
		        return false;
	        }


	        public virtual IEnumerator OnUpkeep(bool playerUpkeep)
	        {
		        yield break;
	        }
	        
	        public virtual bool RespondsToSacrifice(PlayableCard sacrificedcard, PlayableCard cardthatwaspayedfor)
	        {
		        return false;
	        }


	        public virtual IEnumerator OnSacrifice(PlayableCard sacrificedcard, PlayableCard cardthatwaspayedfor)
	        {
		        yield break;
	        }
	        
	        
	        
	        
	        public virtual bool RespondsToCardMoveOut(PlayableCard card, CardSlot fromslot)
	        {
		        return false;
	        }


	        public virtual IEnumerator OnCardMoveOut(PlayableCard card, CardSlot fromslot)
	        {
		        yield break;
	        }
	        
	        public virtual bool RespondsToCardDieHere(PlayableCard card)
	        {
		        return false;
	        }


	        public virtual IEnumerator OnCardDieHere(PlayableCard card)
	        {
		        yield break;
	        }
	        
        }

        public class huntinggrounds : baseslottriggerreciever
        {

	        public override bool RespondsToCardMoveIn(PlayableCard card, CardSlot fromslot)
	        {
		        return true;
	        }


	        public override IEnumerator OnCardMoveIn(PlayableCard card, CardSlot fromslot)
	        {
		        card.Anim.PlayTransformAnimation();
		        Tween.Shake(card.transform, card.slot.transform.GetChild(0).position, new Vector3(5,0,0), 0f, 0.2f, Tween.LoopType.None, null, null, true);
		        var huntgrounds = new CardModificationInfo();
		        huntgrounds.attackAdjustment = 1;
		        huntgrounds.healthAdjustment = 1;
		        huntgrounds.abilities.Add(Ability.GuardDog);
		        card.AddTemporaryMod(huntgrounds);
		        yield break;
	        }
	        
	        public override bool RespondsToCardMoveOut(PlayableCard card, CardSlot fromslot)
	        {
		        return true;
	        }


	        public override IEnumerator OnCardMoveOut(PlayableCard card, CardSlot fromslot)
	        {
		        Log.LogInfo("log1");
		        card.Anim.PlayTransformAnimation();
		        Log.LogInfo("log2");
		        CardModificationInfo whattoremove=null;
		        Log.LogInfo("log3");
		        foreach (var VARIABLE in card.temporaryMods)
		        {
			        Log.LogInfo("log4");
			        if (VARIABLE.abilities.Contains(Ability.GuardDog))
			        {
				        Log.LogInfo("log5");
				        whattoremove=VARIABLE;
			        }
			        Log.LogInfo("log6");
		        }
		        Log.LogInfo("log7");
		        if (whattoremove != null)
		        {
			        Log.LogInfo("log8");
			        card.RemoveTemporaryMod(whattoremove);
			        Log.LogInfo("log8");
		        }
		        Log.LogInfo("log9");
		        
		        yield break;
	        }
	        
        }
        
        
        
        
        
        
        
        
        
        
        
        

        [HarmonyPatch(typeof(PlayerHand), "PlayCardOnSlot")]
        public class patchtheenterboard
        {
            static void Prefix(out PlayerHand __state, ref PlayerHand __instance)
            {
                __state = __instance;
            }

            
            
            public static IEnumerator Postfix(IEnumerator enumerator, PlayableCard card, CardSlot slot, PlayerHand __state)
            {
	            if (card.Info.description.Contains("#enviroment"))
	            {
		            
		            var tests = enviromentcardstypes[enviromentcards.IndexOf(enviromentcards.Find(info => info.name==card.Info.name))];
		            slot.gameObject.AddComponent(tests);
		            Destroy(slot.gameObject.GetComponent(typeof(baseslottriggerreciever)));
		            
		            
		            byte[] imgBytes3 = System.IO.File.ReadAllBytes(staticpath.Replace("enviroments.dll", "")+ "\\lands\\" + card.Info.name.ToLower()  + ".png");
		            Texture2D tex3 = new Texture2D(2, 2);
		            tex3.LoadImage(imgBytes3);
		            slot.gameObject.GetComponentInChildren<MeshRenderer>().material.mainTexture = tex3;
		            slot.gameObject.AddComponent<openintherulebook>();
		            slot.gameObject.AddComponent<enviromenttags>().tagsm = card.Info.name;
		            if (card.Info.description.Contains("#wholelane"))
		            {
			            slot.opposingSlot.gameObject.AddComponent(tests);
			            Destroy(slot.opposingSlot.gameObject.GetComponent(typeof(baseslottriggerreciever)));
		            
		            

			            slot.opposingSlot.gameObject.GetComponentInChildren<MeshRenderer>().material.mainTexture = tex3;
			            slot.opposingSlot.gameObject.AddComponent<openintherulebook>();
			            slot.opposingSlot.gameObject.AddComponent<enviromenttags>().tagsm = card.Info.name;
		            }
			            card.Anim.PlayDeathAnimation();

	            }
	            else
	            {
		            if (__state.CardsInHand.Contains(card))
		            {
			            __state.RemoveCardFromHand(card);
			            if (card.TriggerHandler.RespondsToTrigger(Trigger.PlayFromHand, Array.Empty<object>()))
			            {
				            yield return card.TriggerHandler.OnTrigger(Trigger.PlayFromHand, Array.Empty<object>());
			            }
			            yield return Singleton<BoardManager>.Instance.ResolveCardOnBoard(card, slot, 0.1f, null, true);
		            }
		            yield break;
	            }
	            
            }
        }
        
        public class enviromenttags : MonoBehaviour
        {
            public string tagsm;
        }



        [HarmonyPatch(typeof(RuleBookPage), "FillPage")]
        public class descriptionforkindauniquepages2
        {
	        static bool Prefix(ref AbilityPage __instance, string headerText, params object[] otherArgs)
	        {

		        foreach (var card in enviromentcards)
		        {
			         

			        if(card.name.ToLowerInvariant()==headerText.ToLowerInvariant())
			        {
				        
				        lastloadedability = card.name.ToLowerInvariant();
				        __instance.mainAbilityGroup.ShowAbility((Ability)otherArgs[0], false);
				        return false;  
			        }
		        }

		        //lastloadedability = "";
		        return true;
	        }
        }

        public static string lastloadedability;

        
        [HarmonyPatch(typeof(AbilityPageContent), "ShowAbility")]
        public class descriptionforkindauniquepages
        {
	        static bool Prefix(ref AbilityPageContent __instance, Ability ability, bool fillerAbility)
	        {
		        foreach (var card in enviromentcards)
		        {
			        if (lastloadedability == card.name.ToLowerInvariant())
			        {
				        AbilityInfo info = AbilitiesUtil.GetInfo(ability);
				        __instance.nameTextMesh.text = card.displayedName;
				        string text = enviromentcardsrulebookdescription[enviromentcards.IndexOf(enviromentcards.Find(card => card.name.ToLowerInvariant()==lastloadedability))];
				        __instance.descriptionTextMesh.text = text;
				        __instance.iconRenderer.material.mainTexture = AbilitiesUtil.LoadAbilityIcon(Ability.GuardDog.ToString(), false, false);
				        return false;  
			        }
		        }
		        return true;
		        
	        }
        }
        
        //CardAppearanceBehaviour


        [HarmonyPatch(typeof(TerrainBackground), "ApplyAppearance")]
        public class chanethebackground
        {
	        static bool Prefix(ref CardAppearanceBehaviour __instance)
	        {
		        if (__instance.Card.Info.description.Contains("#enviroment"))
		        {
			        byte[] imgBytes3 = System.IO.File.ReadAllBytes(staticpath.Replace("enviroments.dll", "")+ "\\lands\\EnviromentBackground.png");
			        Texture2D tex3 = new Texture2D(2, 2);
			        tex3.LoadImage(imgBytes3);
			        __instance.Card.RenderInfo.baseTextureOverride = tex3;
			        return false;
		        }
		        return true;
	        }
        }

        public void huntgrounds()
                 {
         	        CardInfo cardInfo= new CardInfo();
         	        cardInfo.name = "HuntingGrounds";
         	        cardInfo.displayedName = "Hunting Grounds";
         	        cardInfo.cost = 2;
         	        byte[] imgBytes3 = System.IO.File.ReadAllBytes(staticpath.Replace("enviroments.dll", "")+ "\\lands\\HuntGrounds.png");
         	        Texture2D tex3 = new Texture2D(2, 2);
         	        tex3.LoadImage(imgBytes3);
         	        cardInfo.portraitTex = Sprite.Create(tex3, new Rect(0.0f, 0.0f, tex3.width, tex3.height), new Vector2(0.5f, 0.5f), 100.0f);
         	        cardInfo.baseAttack = 0;
         	        cardInfo.baseHealth = 0;
         	        cardInfo.hideAttackAndHealth = true;
         	        cardInfo.metaCategories.Add(CardMetaCategory.ChoiceNode);
                    // all tags in description #enviroment a needed thing, #wholelane will place on enemy slot too
         	        cardInfo.description = "This doesnt look like i made this one #enviroment";
         	        cardInfo.appearanceBehaviour.Add(CardAppearanceBehaviour.Appearance.TerrainBackground);
         	        NewCard.Add(cardInfo);
                    enviromentcards.Add(cardInfo);
                    enviromentcardstypes.Add(typeof(huntinggrounds));
                    enviromentcardsrulebookdescription.Add("Creatures in this slot gain hunt sigil, when entering or leaving thi slot gain +1 / +1");
                    
                 }
        
        
        public void boglands()
        {
	        CardInfo cardInfo= new CardInfo();
	        cardInfo.name = "BogLands";
	        cardInfo.displayedName = "Bog Lands";
	        cardInfo.cost = 1;
	        byte[] imgBytes3 = System.IO.File.ReadAllBytes(staticpath.Replace("enviroments.dll", "")+ "\\lands\\HuntGrounds.png");
	        Texture2D tex3 = new Texture2D(2, 2);
	        tex3.LoadImage(imgBytes3);
	        cardInfo.portraitTex = Sprite.Create(tex3, new Rect(0.0f, 0.0f, tex3.width, tex3.height), new Vector2(0.5f, 0.5f), 100.0f);
	        cardInfo.baseAttack = 0;
	        cardInfo.baseHealth = 0;
	        cardInfo.hideAttackAndHealth = true;
	        cardInfo.metaCategories.Add(CardMetaCategory.ChoiceNode);
	        cardInfo.description = "This doesnt look like i made this one";
	        cardInfo.appearanceBehaviour.Add(CardAppearanceBehaviour.Appearance.TerrainBackground);
	        NewCard.Add(cardInfo);
	        enviromentcards.Add(cardInfo);
	        enviromentcardstypes.Add(typeof(huntinggrounds));
	        enviromentcardsrulebookdescription.Add("Creatures in this slot gain hunt sigil, when entering or leaving thi slot gain +1 / +1");
        }


        [HarmonyPatch(typeof(RuleBookController), "Start")]
        public class dotherulebookforenvs
        {
	        static void Postfix(ref RuleBookController __instance)
	        {
		        foreach (var card in enviromentcards)
		        {
			        var page = new RuleBookPageInfo();
			        page.abilityPage = RuleBookController.Instance.PageData[1].abilityPage;
			        page.ability = RuleBookController.Instance.PageData[1].ability;
			        page.additivePrefabs = RuleBookController.Instance.PageData[1].additivePrefabs;
			        page.headerText = card.name;
			        page.pageId = card.name;
			        page.pagePrefab = RuleBookController.Instance.PageData[1].pagePrefab;
			        page.fillerAbilityIds = RuleBookController.Instance.PageData[1].fillerAbilityIds;
			        __instance.PageData.Add(page);
		        }
		        {
			        var page = new RuleBookPageInfo();
			        page.abilityPage = RuleBookController.Instance.PageData[1].abilityPage;
			        page.ability = RuleBookController.Instance.PageData[1].ability;
			        page.additivePrefabs = RuleBookController.Instance.PageData[1].additivePrefabs;
			        page.headerText = "This is ending page";
			        page.pageId = "This is ending page";
			        page.pagePrefab = RuleBookController.Instance.PageData[1].pagePrefab;
			        page.fillerAbilityIds = RuleBookController.Instance.PageData[1].fillerAbilityIds;
			        __instance.PageData.Add(page);
		        }
		        
	        }
        }

        //ConstructPageData
        public class openintherulebook : AlternateInputInteractable
        {
	        public override CursorType CursorType
	        {
		        get
		        {
			        return CursorType.Inspect;
		        }
	        }
	        
	        
	        public Vector3 OriginalLocalPosition { get; private set; }
	        
	        private void Awake()
	        {
		        this.OriginalLocalPosition = base.transform.localPosition;
	        }
	        
	        
	        
	        public override void OnAlternateSelectStarted()
	        {
		        RuleBookController.Instance.SetShown(true, RuleBookController.Instance.OffsetViewForCard(gameObject.GetComponent<CardSlot>().Card));
		        int pageIndex = RuleBookController.Instance.PageData.IndexOf(RuleBookController.Instance.PageData.Find((RuleBookPageInfo x) => x.headerText==base.gameObject.GetComponent<enviromenttags>().tagsm));
		        base.StopAllCoroutines();
		        base.StartCoroutine(RuleBookController.Instance.flipper.FlipToPage(pageIndex, false ? 0f : 0.2f));
	        }
        }


        

        private void Awake()
        {
	        staticpath=Info.Location;
            Plugin.Log = base.Logger;
            Harmony harmony = new Harmony(PluginGuid);
            harmony.PatchAll();
            huntgrounds();
            boglands();
        }
    }
    
}