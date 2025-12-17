using System;
using System.Collections.Generic;
using PetPet.Domain.Enums;

namespace PetPet.Application.Services
{
    public class ZiweiService
    {
        public ZiweiStar CalculateStar(DateTime birthday)
        {
            // Simplified Simulation Algo: (Year + Month + Day) % 14
            // In real world, this depends on Lunar Calendar and Birth Hour.
            int sum = birthday.Year + birthday.Month + birthday.Day;
            int index = sum % 14;
            return (ZiweiStar)index;
        }

        public (string Name, string Description, string Personality, string MatchTips) GetAnalysis(ZiweiStar star)
        {
            return star switch
            {
                ZiweiStar.Ziwei => ("紫微星", "帝王之星", "氣質尊貴，有領導力，但偶爾會比較自我中心。", "適合尋找能輔佐您的「天相」或「天府」。"),
                ZiweiStar.Tianji => ("天機星", "智多星", "聰明反應快，喜歡動腦，但有時容易想太多。", "適合個性互補，穩重的伴侶。"),
                ZiweiStar.Taiyang => ("太陽星", "發電機", "熱情博愛，喜歡照顧人，像太陽一樣燃燒自己。", "適合懂得欣賞您付出的對象。"),
                ZiweiStar.Wuqu => ("武曲星", "財星", "剛毅果決，執行力強，對數字敏感。", "適合溫柔體貼，能包容您剛直個性的伴侶。"),
                ZiweiStar.TianTong => ("天同星", "福星", "個性溫和，知足常樂，有點孩子氣。", "適合能照顧您，或與您一起玩樂的伴侶。"),
                ZiweiStar.Lianzhen => ("廉貞星", "公關星", "能言善道，異性緣佳，有些高傲。", "需要一個能鎮得住您，又懂您內心的伴侶。"),
                ZiweiStar.Tianfu => ("天府星", "庫星", "穩重踏實，包容力強，喜歡享受生活。", "是所有人的好伴侶，特別適合強勢的主星。"),
                ZiweiStar.Taiyin => ("太陰星", "月亮", "溫柔細膩，重視家庭，有點潔癖。", "適合成熟穩重，能給您安全感的對象。"),
                ZiweiStar.Tanlang => ("貪狼星", "桃花星", "多才多藝，長袖善舞，喜歡新鮮感。", "適合能與您一起探索世界，不拘小節的伴侶。"),
                ZiweiStar.Jumen => ("巨門星", "暗星", "觀察力敏銳，口才好，但容易多疑。", "適合心胸寬大，不與您計較口舌之爭的伴侶。"),
                ZiweiStar.Tianxiang => ("天相星", "印星", "忠誠敦厚，更是外貌協會會長，重視儀表。", "適合外型亮眼，且同樣重視生活品質的對象。"),
                ZiweiStar.Tianliang => ("天梁星", "老人星", "成熟穩重，喜歡照顧人，有大哥大姐風範。", "適合依賴性較強，需要被呵護的伴侶。"),
                ZiweiStar.Qishang => ("七殺星", "將軍", "敢愛敢恨，行動力強，不喜歡拖泥帶水。", "適合能理解您衝勁，並默默支持的伴侶。"),
                ZiweiStar.Pojun => ("破軍星", "先鋒", "求新求變，不按牌理出牌，喜歡挑戰。", "需要一個心臟夠強，能陪您冒險的伴侶。"),
                _ => ("未知", "神秘", "充滿無限可能。", "一切隨緣。")
            };
        }

        public int CalculateMatchScore(ZiweiStar starA, ZiweiStar starB)
        {
            // Simple random-ish logic based on enum distance for demo
            // In reality, specific pairs like Ziwei+Tianfu are 100
            
            // Perfect Matches
            if ((starA == ZiweiStar.Ziwei && starB == ZiweiStar.Tianfu) ||
                (starA == ZiweiStar.Tianfu && starB == ZiweiStar.Ziwei) ||
                (starA == ZiweiStar.Taiyang && starB == ZiweiStar.Taiyin) ||
                (starA == ZiweiStar.Taiyin && starB == ZiweiStar.Taiyang))
            {
                return 98;
            }

            int diff = Math.Abs((int)starA - (int)starB);
            if (diff == 0) return 90; // Same star is usually okay
            if (diff % 2 == 0) return 85; 
            return 75; // Base score
        }
    }
}
