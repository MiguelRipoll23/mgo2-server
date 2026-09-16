/* containing function of static patch target(s); entry .opd.FUN_00d81860 @ 00d81860 */

void _opd_FUN_00d81860(byte *param_1,uint param_2)

{
  undefined1 *puVar1;
  uint uVar2;
  int iVar3;
  uint uVar4;
  uint uVar5;
  longlong lVar6;
  uint uVar7;
  
  puVar1 = *(undefined1 **)(PTR_PTR_0121c0c0 + -0x8000);
  puVar1[3] = 0x90;
  *puVar1 = 0x8b;
  puVar1[1] = 0x75;
  puVar1[2] = 0x2c;
  puVar1[4] = 0x3a;
  puVar1[5] = 0x5e;
  puVar1[6] = 0x4d;
  puVar1[7] = 0xf1;
  if (0 < (int)param_2) {
    lVar6 = ((ulonglong)param_2 - 1 & 0xffffffff) + 1;
    uVar4 = 0;
    uVar5 = 0;
    *param_1 = *param_1 ^ 0x8b;
    while (lVar6 = lVar6 + -1, lVar6 != 0) {
      while( true ) {
        uVar5 = uVar5 + 1;
        iVar3 = uVar5 + (((int)uVar5 >> 2) + (uint)((int)uVar5 < 0 && (uVar5 & 3) != 0)) * -4;
        param_1[uVar5] = param_1[uVar5] ^ puVar1[uVar4 + iVar3];
        if (iVar3 != 3) break;
        uVar7 = uVar4 + 1 ^ 5;
        uVar2 = (int)uVar7 >> 0x1f;
        uVar4 = uVar4 + 1 & (int)(uVar2 - (uVar2 ^ uVar7)) >> 0x1f;
        lVar6 = lVar6 + -1;
        if (lVar6 == 0) {
          return;
        }
      }
    }
  }
  return;
}

