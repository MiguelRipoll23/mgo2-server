// original call TARGET 0x00ba08d8

/* WARNING: Type propagation algorithm not settling */

void _opd_FUN_00ba08d8(int param_1,int param_2)

{
  bool bVar1;
  byte bVar2;
  char cVar3;
  undefined4 uVar4;
  undefined4 uVar5;
  undefined4 uVar6;
  undefined4 uVar7;
  undefined4 uVar8;
  undefined4 uVar9;
  undefined4 uVar10;
  uint uVar11;
  uint uVar12;
  undefined4 uVar13;
  longlong lVar14;
  uint uVar15;
  undefined *puVar16;
  undefined4 uVar17;
  uint *puVar18;
  uint *puVar19;
  uint *puVar20;
  undefined4 uVar21;
  int iVar22;
  int iVar23;
  byte *pbVar24;
  int iVar25;
  undefined4 *puVar26;
  longlong lVar27;
  longlong lVar28;
  undefined1 auStack_150 [2];
  undefined2 local_14e;
  short local_14c;
  undefined1 auStack_14a [2];
  short local_148;
  undefined1 auStack_146 [34];
  undefined4 local_124 [4];
  undefined4 local_114;
  undefined4 local_110;
  undefined4 local_10c;
  undefined4 local_108;
  undefined4 local_104 [4];
  undefined4 local_f4;
  undefined4 local_f0;
  undefined4 local_ec;
  undefined4 local_e8;
  undefined4 local_e4 [4];
  undefined4 local_d4;
  undefined4 local_d0;
  undefined4 local_cc;
  undefined4 local_c8;
  undefined4 auStack_c4 [19];
  
  puVar16 = PTR_DAT_0121bd78;
  iVar22 = *(int *)(PTR_DAT_0121bd78 + -0x7ffc);
  local_e4[1] = *(undefined4 *)(iVar22 + 0x21c);
  local_e4[2] = *(undefined4 *)(iVar22 + 0x220);
  local_e4[3] = *(undefined4 *)(iVar22 + 0x224);
  local_d4 = *(undefined4 *)(iVar22 + 0x228);
  local_d0 = *(undefined4 *)(iVar22 + 0x22c);
  local_cc = *(undefined4 *)(iVar22 + 0x230);
  local_c8 = *(undefined4 *)(iVar22 + 0x234);
  local_104[0] = *(undefined4 *)(iVar22 + 0x238);
  local_104[1] = *(undefined4 *)(iVar22 + 0x23c);
  local_104[2] = *(undefined4 *)(iVar22 + 0x240);
  local_104[3] = *(undefined4 *)(iVar22 + 0x244);
  local_f4 = *(undefined4 *)(iVar22 + 0x248);
  local_f0 = *(undefined4 *)(iVar22 + 0x24c);
  local_ec = *(undefined4 *)(iVar22 + 0x250);
  local_e4[0] = *(undefined4 *)(iVar22 + 0x218);
  local_124[0] = *(undefined4 *)(iVar22 + 600);
  local_e8 = *(undefined4 *)(iVar22 + 0x254);
  uVar4 = *(undefined4 *)(iVar22 + 0x294);
  local_124[2] = *(undefined4 *)(iVar22 + 0x260);
  local_124[1] = *(undefined4 *)(iVar22 + 0x25c);
  local_114 = *(undefined4 *)(iVar22 + 0x268);
  local_110 = *(undefined4 *)(iVar22 + 0x26c);
  local_10c = *(undefined4 *)(iVar22 + 0x270);
  local_108 = *(undefined4 *)(iVar22 + 0x274);
  uVar5 = *(undefined4 *)(iVar22 + 0x278);
  uVar17 = *(undefined4 *)(iVar22 + 0x27c);
  uVar6 = *(undefined4 *)(iVar22 + 0x280);
  uVar7 = *(undefined4 *)(iVar22 + 0x284);
  uVar8 = *(undefined4 *)(iVar22 + 0x288);
  uVar9 = *(undefined4 *)(iVar22 + 0x28c);
  uVar10 = *(undefined4 *)(iVar22 + 0x290);
  local_124[3] = *(undefined4 *)(iVar22 + 0x264);
  iVar23 = *(int *)(param_1 + 0x8c);
  iVar25 = *(int *)(param_1 + 0xa4);
  if (param_2 == 4) {
    if (*(char *)(param_1 + 0x92) == '\0') {
      uVar13 = *(undefined4 *)(param_1 + 100);
      uVar21 = _opd_FUN_0023e4a0(0xf914bf,0xf2e666);
      _opd_FUN_00b310a0(uVar13,0x1c26e5,uVar21,0);
      _opd_FUN_00b30880(*(undefined4 *)(param_1 + 100),0x1b4c98);
    }
    uVar13 = *(undefined4 *)(param_1 + 100);
    uVar21 = _opd_FUN_0023e4a0(0xf914bf,&DAT_002ca1fe);
    _opd_FUN_00b30e60(uVar13,0xc7b117,uVar21,0);
    uVar13 = *(undefined4 *)(param_1 + 100);
    uVar21 = _opd_FUN_0023e4a0(0xf914bf,&DAT_002ca1fe);
    _opd_FUN_00b30e60(uVar13,&DAT_00b9f9b4,uVar21,0);
    _opd_FUN_00b04a78(*(undefined4 *)(param_1 + 100),&DAT_003d8095);
    _opd_FUN_00b04ad8(*(undefined4 *)(param_1 + 100),0x3e59b9);
  }
  if (*(char *)(iVar23 + iVar25 + 0x10) == '\0') {
    _opd_FUN_00b30830(*(undefined4 *)(param_1 + 100),uVar5);
    cVar3 = *(char *)(iVar23 + iVar25 + 0x11);
  }
  else {
    _opd_FUN_00b30880(*(undefined4 *)(param_1 + 100),uVar5);
    cVar3 = *(char *)(iVar23 + iVar25 + 0x11);
  }
  if (cVar3 == '\0') {
    _opd_FUN_00b30830(*(undefined4 *)(param_1 + 100),uVar17);
    cVar3 = *(char *)(iVar23 + iVar25 + 0x12);
  }
  else {
    _opd_FUN_00b30880(*(undefined4 *)(param_1 + 100),uVar17);
    cVar3 = *(char *)(iVar23 + iVar25 + 0x12);
  }
  if (cVar3 == '\0') {
    _opd_FUN_00b30830(*(undefined4 *)(param_1 + 100),uVar6);
    cVar3 = *(char *)(iVar23 + iVar25 + 0x13);
  }
  else {
    _opd_FUN_00b30880(*(undefined4 *)(param_1 + 100),uVar6);
    cVar3 = *(char *)(iVar23 + iVar25 + 0x13);
  }
  if (cVar3 == '\0') {
    _opd_FUN_00b30830(*(undefined4 *)(param_1 + 100),uVar7);
    cVar3 = *(char *)(iVar23 + iVar25 + 0x14);
  }
  else {
    _opd_FUN_00b30880(*(undefined4 *)(param_1 + 100),uVar7);
    cVar3 = *(char *)(iVar23 + iVar25 + 0x14);
  }
  if (cVar3 == '\0') {
    _opd_FUN_00b30830(*(undefined4 *)(param_1 + 100),uVar8);
    cVar3 = *(char *)(iVar23 + iVar25 + 0x15);
  }
  else {
    _opd_FUN_00b30880(*(undefined4 *)(param_1 + 100),uVar8);
    cVar3 = *(char *)(iVar23 + iVar25 + 0x15);
  }
  if (cVar3 == '\0') {
    _opd_FUN_00b30830(*(undefined4 *)(param_1 + 100),uVar9);
    cVar3 = *(char *)(iVar23 + iVar25 + 0x16);
  }
  else {
    _opd_FUN_00b30880(*(undefined4 *)(param_1 + 100),uVar9);
    cVar3 = *(char *)(iVar23 + iVar25 + 0x16);
  }
  if (cVar3 == '\0') {
    _opd_FUN_00b30830(*(undefined4 *)(param_1 + 100),uVar10);
  }
  else {
    _opd_FUN_00b30880(*(undefined4 *)(param_1 + 100),uVar10);
  }
  if (*(char *)(iVar23 + iVar25 + 0x17) == '\0') {
    _opd_FUN_00b30830(*(undefined4 *)(param_1 + 100),uVar4);
  }
  else {
    _opd_FUN_00b30880(*(undefined4 *)(param_1 + 100),uVar4);
  }
  pbVar24 = (byte *)(iVar23 + iVar25);
  puVar26 = (undefined4 *)(param_1 + 0x68);
  iVar23 = 0;
  do {
    iVar25 = iVar25 + 1;
    _opd_FUN_00b1c3e8(*(undefined4 *)(param_1 + 100),*(undefined4 *)((int)local_e4 + iVar23),iVar25,
                      0);
    bVar2 = *pbVar24;
    uVar4 = *(undefined4 *)(param_1 + 100);
    uVar5 = *(undefined4 *)((int)local_104 + iVar23);
    _opd_FUN_00f9ac38(auStack_c4,iVar22 + 0x20,0x44);
    if ((bVar2 < 0x11) && (2 < bVar2 - 9)) {
      uVar17 = _opd_FUN_00b30bb0(auStack_c4[bVar2]);
      _opd_FUN_00b30e60(uVar4,uVar5,uVar17,0);
      bVar2 = pbVar24[0x10];
      uVar4 = *(undefined4 *)(param_1 + 100);
      uVar5 = *(undefined4 *)((int)local_124 + iVar23);
    }
    else {
      _opd_FUN_00b30e60(uVar4,uVar5,0,0);
      bVar2 = pbVar24[0x10];
      uVar4 = *(undefined4 *)(param_1 + 100);
      uVar5 = *(undefined4 *)((int)local_124 + iVar23);
    }
    uVar15 = (uint)bVar2;
    if ((uVar15 < 0x11) && (uVar15 < 0x10)) {
                    /* WARNING: Could not recover jumptable at 0x00ba0c68. Too many branches */
                    /* WARNING: Treating indirect jump as call */
      (*(code *)(*(int *)(uVar15 * 4 + *(int *)(puVar16 + -0x7fd8)) + *(int *)(puVar16 + -0x7fd8)))
                ();
      return;
    }
    _opd_FUN_00b30e60(uVar4,uVar5,0,0);
    if (pbVar24[0x20] == 2) {
      _opd_FUN_00b30880(*puVar26,0x6335f4);
      _opd_FUN_00b04ad8(*puVar26,&DAT_0042fae1);
      _opd_FUN_002438b8(*puVar26);
    }
    else if (pbVar24[0x20] == 4) {
      _opd_FUN_00b30880(*puVar26,0x6335f4);
      _opd_FUN_00b04ad8(*puVar26,0x12836);
      _opd_FUN_002438b8(*puVar26);
    }
    else {
      _opd_FUN_00b30830(*puVar26,0x6335f4);
      _opd_FUN_002438e0(*puVar26);
    }
    bVar1 = iVar23 != 0x1c;
    pbVar24 = pbVar24 + 1;
    puVar26 = puVar26 + 1;
    iVar23 = iVar23 + 4;
  } while (bVar1);
  uVar4 = *(undefined4 *)(param_1 + 100);
  uVar15 = *(uint *)(param_1 + 0xa4);
  uVar11 = *(uint *)(param_1 + 0x94);
  puVar18 = (uint *)_opd_FUN_00242bf0(uVar4,0xd902af);
  puVar19 = (uint *)_opd_FUN_00242bf0(uVar4,&DAT_00d81123);
  puVar20 = (uint *)_opd_FUN_00242bf0(uVar4,0x848a1c);
  if (puVar18 == (uint *)0x0) {
    return;
  }
  if (puVar19 == (uint *)0x0) {
    return;
  }
  if (puVar20 == (uint *)0x0) {
    return;
  }
  if (8 < uVar11) {
    if ((*puVar18 & 0x400000) == 0) {
      *puVar18 = *puVar18 | 0x40400000;
      uVar12 = *puVar19;
    }
    else {
      uVar12 = *puVar19;
    }
    if ((uVar12 & 0x400000) == 0) {
      *puVar19 = uVar12 | 0x40400000;
      uVar12 = *puVar20;
    }
    else {
      uVar12 = *puVar20;
    }
    if ((uVar12 & 0x400000) == 0) {
      *puVar20 = uVar12 | 0x40400000;
    }
    _opd_FUN_002449e8(puVar20,auStack_150,&local_148,auStack_14a,&local_14c);
    lVar27 = (longlong)((int)local_14c - (int)local_148) / (longlong)(ulonglong)uVar11;
    lVar14 = lVar27 * (ulonglong)uVar15;
    _opd_FUN_00253de0(puVar18,&local_14e,auStack_146,0,0);
    lVar28 = (longlong)((int)local_14c - (int)local_148) + lVar27 * -8;
    _opd_FUN_00253be0(puVar18,local_14e,
                      (short)lVar27 * 8 + ((ushort)lVar28 & (ushort)(lVar28 >> 0x3f)),0,0);
    _opd_FUN_00253de0(puVar19,&local_14e,auStack_146,0,0);
    _opd_FUN_00253be0(puVar19,local_14e,(ushort)lVar14 & (ushort)(-lVar14 >> 0x3f),0,0);
    return;
  }
  puVar18 = (uint *)_opd_FUN_00242bf0(uVar4,0xd902af);
  puVar19 = (uint *)_opd_FUN_00242bf0(uVar4,&DAT_00d81123);
  iVar22 = _opd_FUN_00242bf0(uVar4,0x848a1c);
  if (puVar18 == (uint *)0x0) {
    return;
  }
  if (puVar19 != (uint *)0x0) {
    if (iVar22 == 0) {
      return;
    }
    if ((*puVar18 & 0x400000) != 0) {
      *puVar18 = *puVar18 & 0xffbfffff | 0x40000000;
    }
    if ((*puVar19 & 0x400000) != 0) {
      *puVar19 = *puVar19 & 0xffbfffff | 0x40000000;
      return;
    }
    return;
  }
  return;
}

