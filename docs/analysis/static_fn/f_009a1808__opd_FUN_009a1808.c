/* containing function of static patch target(s); entry .opd.FUN_009a1808 @ 009a1808 */

void _opd_FUN_009a1808(int param_1)

{
  bool bVar1;
  int *piVar2;
  undefined *puVar3;
  byte bVar4;
  undefined8 uVar5;
  int iVar6;
  undefined4 uVar7;
  undefined1 uVar11;
  short sVar10;
  char cVar12;
  int iVar8;
  uint uVar9;
  ulonglong uVar13;
  uint uVar14;
  int iVar15;
  char *pcVar16;
  char *pcVar17;
  byte *pbVar18;
  longlong lVar19;
  undefined4 local_840;
  undefined1 local_83c [12];
  undefined1 local_830 [16];
  undefined4 local_820;
  undefined1 auStack_81c [988];
  undefined1 auStack_440 [768];
  char acStack_140 [232];
  
  puVar3 = PTR_PTR_0121bb04;
  uVar14 = *(int *)(param_1 + 0x68) + 5;
  *(uint *)(param_1 + 0x68) = uVar14;
  if (12000 < uVar14) {
    uVar7 = *(undefined4 *)(puVar3 + -0x7fb4);
    uVar5 = 0xf01;
    iVar6 = -0xa0;
LAB_009a25d8:
    _opd_FUN_0097f1b8(uVar5,iVar6,uVar7);
    return;
  }
  piVar2 = *(int **)(puVar3 + -0x8000);
  iVar6 = _opd_FUN_00f0d468(*(undefined4 *)(*piVar2 + 0x60));
  if (iVar6 == 0) {
    return;
  }
  _opd_FUN_00a2059c();
  iVar6 = _opd_FUN_00f0d0cc(*(undefined4 *)(*piVar2 + 0x60));
  if (iVar6 == 0) {
    uVar7 = _opd_FUN_00f0ccbc(*(undefined4 *)(*piVar2 + 0x60));
    _opd_FUN_00f9ac38(param_1 + 0x6c,uVar7,0x3dc);
    *(undefined4 *)(param_1 + 0x6c) = 1;
  }
  else {
    if (iVar6 != -0x1fa) {
      uVar7 = *(undefined4 *)(puVar3 + -0x7fb4);
      uVar5 = 0xf02;
      goto LAB_009a25d8;
    }
    _opd_FUN_00fa4d48(param_1 + 0x6c,0,0x3dc);
    uVar7 = _opd_FUN_0027e2b0(0x19);
    _opd_FUN_0027e480(uVar7,0x8c,0x10,param_1 + 0x70);
    *(undefined1 *)(param_1 + 0x3ba) = 0;
    _opd_FUN_000d60b8(param_1 + 0x81,0x81);
    uVar7 = _opd_FUN_00a0c930(0x1b3);
    _opd_FUN_00f9e1b8(param_1 + 0x81,uVar7,0x80);
    *(undefined1 *)(param_1 + 0x35e) = 5;
    *(undefined1 *)(param_1 + 0x36f) = 7;
    *(undefined1 *)(param_1 + 0x360) = 4;
    *(undefined1 *)(param_1 + 0x36d) = 4;
    *(undefined1 *)(param_1 + 0x35f) = 2;
    *(undefined1 *)(param_1 + 0x370) = 0xc;
    *(undefined1 *)(param_1 + 0x36c) = 2;
    *(undefined1 *)(param_1 + 0x35c) = 1;
    *(undefined1 *)(param_1 + 0x37c) = 0;
    *(undefined1 *)(param_1 + 0x35d) = 3;
    *(undefined1 *)(param_1 + 0x37d) = 0;
    *(undefined1 *)(param_1 + 0x36e) = 3;
    *(undefined1 *)(param_1 + 0x37e) = 0;
    *(undefined1 *)(param_1 + 0x37f) = 0;
    *(undefined1 *)(param_1 + 0x380) = 0;
    _opd_FUN_00fa4d48(param_1 + 0x38e,0,0x10);
    *(undefined1 *)(param_1 + 0x39e) = 0x10;
    *(undefined1 *)(param_1 + 0x405) = 3;
    *(ulonglong *)(param_1 + 0x408) =
         *(ulonglong *)(param_1 + 0x408) & 0xffffffffff7fe000 | 0x19e000 |
         *(ulonglong *)(param_1 + 0x408) & 0xfff;
    *(undefined4 *)(param_1 + 0x3a0) = 2;
    *(undefined1 *)(param_1 + 0x404) = 2;
    *(undefined2 *)(param_1 + 0x410) = 5;
    *(undefined2 *)(param_1 + 0x412) = 3;
    uVar11 = _opd_FUN_0075e000();
    *(undefined1 *)(param_1 + 0x3bb) = uVar11;
    uVar7 = _opd_FUN_0075e000();
    *(undefined4 *)(param_1 + 0x3bc) = uVar7;
    *(undefined1 *)(param_1 + 0x41a) = 5;
    *(undefined1 *)(param_1 + 0x418) = 1;
    *(undefined4 *)(param_1 + 0x3ec) = 5;
    *(ulonglong *)(param_1 + 0x408) =
         *(ulonglong *)(param_1 + 0x408) & 0xffffffffff800000 |
         *(ulonglong *)(param_1 + 0x408) & 0x3ffbff | 0xb00 | 0x240000;
    *(undefined4 *)(param_1 + 0x3f4) = 7;
    *(undefined4 *)(param_1 + 1000) = 0x1e;
    *(undefined4 *)(param_1 + 0x400) = 2;
    *(undefined4 *)(param_1 + 0x3e0) = 0x32;
    *(undefined1 *)(param_1 + 0x41c) = 3;
    *(undefined4 *)(param_1 + 0x3c8) = 4;
    *(undefined1 *)(param_1 + 0x423) = 0;
    *(undefined4 *)(param_1 + 0x3fc) = 10;
    *(undefined4 *)(param_1 + 0x3e4) = 5;
    *(undefined1 *)(param_1 + 0x41b) = 2;
    *(undefined4 *)(param_1 + 0x3d8) = 5;
    *(undefined4 *)(param_1 + 0x3dc) = 2;
    *(undefined4 *)(param_1 + 0x3d0) = 7;
    *(undefined4 *)(param_1 + 0x3d4) = 2;
    *(undefined4 *)(param_1 + 0x3cc) = 2;
    *(undefined4 *)(param_1 + 0x3c0) = 7;
    *(undefined4 *)(param_1 + 0x3c4) = 2;
    *(undefined1 *)(param_1 + 0x419) = 3;
    *(undefined4 *)(param_1 + 0x3f0) = 2;
    *(undefined4 *)(param_1 + 0x3f8) = 2;
  }
  if (*(char *)(param_1 + 0x113) != '\0') {
    *(char *)(param_1 + 0x39e) = *(char *)(param_1 + 0x39e) + -1;
  }
  iVar6 = _opd_FUN_00280418();
  if (iVar6 == 0) goto LAB_009a25b0;
  if (*(char *)(param_1 + 0x70) == '\0') {
    uVar7 = _opd_FUN_0027e2b0(0x19);
    _opd_FUN_0027e480(uVar7,0x8c,0x10,(char *)(param_1 + 0x70));
  }
  if (0xb < *(byte *)(param_1 + 0x3ba)) {
    *(undefined1 *)(param_1 + 0x3ba) = 0;
  }
  pbVar18 = (byte *)(param_1 + 0x35c);
  pcVar16 = (char *)(param_1 + 0x37c);
  pcVar17 = (char *)(param_1 + 0x36c);
  iVar15 = 0;
  do {
    bVar4 = *pbVar18;
    sVar10 = _opd_FUN_00a0c3ec(bVar4);
    if (sVar10 == -1) {
      *pbVar18 = 1;
    }
    if (((bVar4 < 0x11) && (bVar4 != 0xe)) && (bVar4 != 8)) {
      uVar5 = 7;
      if (((bVar4 != 0xc) && (uVar5 = 0xb, bVar4 != 0xf)) &&
         ((uVar5 = 10, bVar4 != 6 && ((uVar5 = 0xc, bVar4 != 0x10 && (uVar5 = 0, bVar4 != 7)))))) {
        if (bVar4 != 0xd) goto LAB_009a1c30;
        uVar5 = 8;
      }
      cVar12 = _opd_FUN_00f04238(iVar6,uVar5);
      if (cVar12 == '\0') {
        *pbVar18 = 1;
      }
    }
    else {
      *pbVar18 = 1;
    }
LAB_009a1c30:
    if ((*pcVar17 != '\0') && (sVar10 = _opd_FUN_00a0cd94(*pbVar18,*pcVar17), sVar10 == -1)) {
      cVar12 = _opd_FUN_00a0ce98(*pbVar18,0);
      *pcVar17 = cVar12;
    }
    if (*pcVar16 != '\0') {
      cVar12 = _opd_FUN_00a0ccb8(bVar4,*pcVar16);
      if (cVar12 == '\0') {
        *pcVar16 = '\0';
      }
      else if ((*pcVar16 == '\x04') &&
              ((cVar12 = _opd_FUN_00f04238(iVar6,0), cVar12 == '\0' ||
               (iVar8 = _opd_FUN_0097d170(), iVar8 != 0)))) {
        *pcVar16 = '\0';
      }
    }
    bVar1 = iVar15 != 0xf;
    pbVar18 = pbVar18 + 1;
    pcVar16 = pcVar16 + 1;
    pcVar17 = pcVar17 + 1;
    iVar15 = iVar15 + 1;
  } while (bVar1);
  if (*(char *)(param_1 + 0x113) != '\0') {
    *(undefined1 *)(param_1 + 0x113) = 1;
  }
  bVar4 = *(byte *)(param_1 + 0x39e);
  if ((bVar4 < 2) || (0x10 < bVar4)) {
    *(undefined1 *)(param_1 + 0x39e) = 0x10;
  }
  else if ((bVar4 & 1) != 0) {
    *(byte *)(param_1 + 0x39e) = bVar4 - 1;
  }
  if (0x1d < *(int *)(param_1 + 0x3a0) - 1U) {
    *(undefined4 *)(param_1 + 0x3a0) = 2;
  }
  *(uint *)(param_1 + 0x40c) = *(uint *)(param_1 + 0x40c) | 0x40000;
  cVar12 = _opd_FUN_00f04238(iVar6,4);
  if ((cVar12 == '\0') || ((*(uint *)(param_1 + 0x40c) >> 0x15 & 1) != 0)) {
    *(uint *)(param_1 + 0x40c) = *(uint *)(param_1 + 0x40c) | 0x200000;
  }
  iVar6 = _opd_FUN_00280418();
  if (iVar6 != 0) {
    if ((*(uint *)(param_1 + 0x40c) >> 0x17 & 1) != 0) {
      *(uint *)(param_1 + 0x40c) = *(uint *)(param_1 + 0x40c) | 0x800000;
    }
    cVar12 = *(char *)(param_1 + 0x404);
    if (cVar12 < '\0') {
      iVar6 = _opd_FUN_0097cd48();
      if ((iVar6 != 0) && (-1 < (char)*(byte *)(param_1 + 0x405))) {
        bVar4 = *(byte *)(param_1 + 0x405) | 0x80;
LAB_009a20e4:
        *(byte *)(param_1 + 0x405) = bVar4;
      }
    }
    else {
      if (((4 < (byte)(cVar12 - 2U)) && (cVar12 != '\t')) &&
         ((cVar12 = _opd_FUN_00f04238(iVar6,0x11), cVar12 == '\0' ||
          (*(char *)(param_1 + 0x404) != '\0')))) {
        *(undefined1 *)(param_1 + 0x404) = 2;
      }
      if (((4 < (byte)(*(char *)(param_1 + 0x405) - 2U)) && (*(char *)(param_1 + 0x405) != '\t')) &&
         ((cVar12 = _opd_FUN_00f04238(iVar6,0x11), cVar12 == '\0' ||
          (*(char *)(param_1 + 0x405) != '\0')))) {
        *(undefined1 *)(param_1 + 0x405) = 3;
      }
      if (*(char *)(param_1 + 0x404) == *(char *)(param_1 + 0x405)) {
        *(undefined1 *)(param_1 + 0x404) = 2;
        *(undefined1 *)(param_1 + 0x405) = 3;
      }
      iVar6 = _opd_FUN_0097cd48();
      if ((iVar6 != 0) &&
         ((cVar12 = *(char *)(param_1 + 0x404), cVar12 == '\0' ||
          (*(char *)(param_1 + 0x405) == '\0')))) {
        _opd_FUN_00f9ac38(auStack_81c,param_1 + 0x6c,0x3dc);
        _opd_FUN_00f9ac38(auStack_440,auStack_81c,0x3dc);
        lVar19 = 0x10;
        pcVar16 = acStack_140;
        do {
          if ((*pcVar16 != '\0') && (pcVar16[-0x10] == '\x04')) {
            uVar13 = 0;
            local_830 = (undefined1  [16])0x0;
            local_820 = 0;
            local_83c = SUB1612((undefined1  [16])0x0,0);
            local_840 = 0xffffffff;
            if (cVar12 == '\x02') {
LAB_009a1fa0:
              if (*(char *)(param_1 + 0x405) != '\x03') {
                lVar19 = uVar13 << 2;
                uVar13 = uVar13 + 1;
                *(undefined4 *)(local_83c + (int)lVar19 + -4) = 3;
              }
              if (cVar12 != '\x04') goto LAB_009a1fcc;
LAB_009a1ff8:
              if (*(char *)(param_1 + 0x405) != '\x05') {
                lVar19 = uVar13 << 2;
                uVar13 = uVar13 + 1;
                *(undefined4 *)(local_83c + (int)lVar19 + -4) = 5;
              }
              if (cVar12 != '\x06') goto LAB_009a2024;
LAB_009a2050:
              uVar14 = (uint)uVar13;
              if (*(char *)(param_1 + 0x405) != '\t') {
                uVar14 = uVar14 + 1;
                *(undefined4 *)(local_83c + (int)(uVar13 << 2) + -4) = 9;
              }
            }
            else {
              bVar1 = *(char *)(param_1 + 0x405) != '\x02';
              if (bVar1) {
                local_840 = 2;
              }
              uVar13 = (ulonglong)bVar1;
              if (cVar12 != '\x03') goto LAB_009a1fa0;
LAB_009a1fcc:
              if (*(char *)(param_1 + 0x405) != '\x04') {
                lVar19 = uVar13 << 2;
                uVar13 = uVar13 + 1;
                *(undefined4 *)(local_83c + (int)lVar19 + -4) = 4;
              }
              if (cVar12 != '\x05') goto LAB_009a1ff8;
LAB_009a2024:
              if (*(char *)(param_1 + 0x405) != '\x06') {
                lVar19 = uVar13 << 2;
                uVar13 = uVar13 + 1;
                *(undefined4 *)(local_83c + (int)lVar19 + -4) = 6;
              }
              uVar14 = (uint)uVar13;
              if (cVar12 != '\t') goto LAB_009a2050;
            }
            if (uVar14 != 0) {
              uVar13 = (longlong)**(int **)(puVar3 + -0x7ffc) * 0x5d588b65 + 1;
              **(int **)(puVar3 + -0x7ffc) = (int)uVar13;
              uVar13 = uVar13 - (longlong)(int)((uVar13 & 0xffffffff) / (ulonglong)uVar14) *
                                (longlong)(int)uVar14;
              if (*(char *)(param_1 + 0x404) == '\0') {
                *(char *)(param_1 + 0x404) =
                     (char)*(undefined4 *)(local_83c + (int)((uVar13 & 0xffffffff) << 2) + -4);
              }
              else if (*(char *)(param_1 + 0x405) == '\0') {
                bVar4 = (byte)*(undefined4 *)(local_83c + (int)((uVar13 & 0xffffffff) << 2) + -4);
                goto LAB_009a20e4;
              }
            }
            break;
          }
          lVar19 = lVar19 + -1;
          pcVar16 = pcVar16 + 1;
        } while (lVar19 != 0);
      }
    }
  }
  if ((*(uint *)(param_1 + 0x40c) >> 0x14 & 1) != 0) {
    *(uint *)(param_1 + 0x40c) = *(uint *)(param_1 + 0x40c) | 0x100000;
  }
  if ((*(uint *)(param_1 + 0x40c) >> 0x13 & 1) != 0) {
    *(uint *)(param_1 + 0x40c) = *(uint *)(param_1 + 0x40c) | 0x80000;
  }
  if ((*(uint *)(param_1 + 0x40c) >> 0x10 & 1) == 0) {
LAB_009a2138:
    *(undefined2 *)(param_1 + 0x410) = 5;
  }
  else {
    *(uint *)(param_1 + 0x40c) = *(uint *)(param_1 + 0x40c) | 0x10000;
    if ((*(ushort *)(param_1 + 0x410) == 0) || (99 < *(ushort *)(param_1 + 0x410)))
    goto LAB_009a2138;
  }
  if ((*(uint *)(param_1 + 0x40c) >> 0xf & 1) == 0) {
LAB_009a2168:
    *(undefined2 *)(param_1 + 0x412) = 3;
  }
  else {
    *(uint *)(param_1 + 0x40c) = *(uint *)(param_1 + 0x40c) | 0x8000;
    if ((*(ushort *)(param_1 + 0x412) == 0) || (99 < *(ushort *)(param_1 + 0x412)))
    goto LAB_009a2168;
  }
  if ((*(uint *)(param_1 + 0x40c) >> 0xe & 1) != 0) {
    *(uint *)(param_1 + 0x40c) = *(uint *)(param_1 + 0x40c) | 0x4000;
  }
  if ((*(uint *)(param_1 + 0x40c) >> 0xd & 1) != 0) {
    *(uint *)(param_1 + 0x40c) = *(uint *)(param_1 + 0x40c) | 0x2000;
  }
  uVar9 = _opd_FUN_0075e000();
  uVar14 = *(uint *)(param_1 + 0x40c);
  if ((uVar14 & 0x1000) == 0) {
    *(char *)(param_1 + 0x3bb) = (char)uVar9;
    *(uint *)(param_1 + 0x3bc) = uVar14 & 0x1000;
  }
  else {
    bVar4 = *(byte *)(param_1 + 0x3bb);
    *(uint *)(param_1 + 0x40c) = uVar14 | 0x1000;
    if (bVar4 == 0) {
      *(byte *)(param_1 + 0x3bb) = bVar4;
    }
    else if (uVar9 < bVar4) {
      *(char *)(param_1 + 0x3bb) = (char)uVar9;
    }
    if (uVar9 < *(uint *)(param_1 + 0x3bc)) {
      *(uint *)(param_1 + 0x3bc) = uVar9;
    }
  }
  if ((*(uint *)(param_1 + 0x40c) >> 0xb & 1) != 0) {
    *(uint *)(param_1 + 0x40c) = *(uint *)(param_1 + 0x40c) | 0x800;
  }
  if ((*(uint *)(param_1 + 0x40c) >> 10 & 1) != 0) {
    *(uint *)(param_1 + 0x40c) = *(uint *)(param_1 + 0x40c) | 0x400;
  }
  if ((*(uint *)(param_1 + 0x40c) >> 9 & 1) != 0) {
    *(uint *)(param_1 + 0x40c) = *(uint *)(param_1 + 0x40c) | 0x200;
  }
  if ((*(uint *)(param_1 + 0x40c) >> 8 & 1) != 0) {
    *(uint *)(param_1 + 0x40c) = *(uint *)(param_1 + 0x40c) | 0x100;
  }
  if ((*(uint *)(param_1 + 0x3e4) < 2) || (99 < *(uint *)(param_1 + 0x3e4))) {
    *(undefined4 *)(param_1 + 0x3e4) = 5;
  }
  if ((*(byte *)(param_1 + 0x41d) == 0) || (0x14 < *(byte *)(param_1 + 0x41d))) {
    *(undefined1 *)(param_1 + 0x41d) = 1;
  }
  if ((*(uint *)(param_1 + 1000) == 0) || (99 < *(uint *)(param_1 + 1000))) {
    *(undefined4 *)(param_1 + 1000) = 0x1e;
  }
  if ((*(byte *)(param_1 + 0x41a) < 3) || (99 < *(byte *)(param_1 + 0x41a))) {
    *(undefined1 *)(param_1 + 0x41a) = 5;
  }
  if ((*(byte *)(param_1 + 0x41b) == 0) || (0x14 < *(byte *)(param_1 + 0x41b))) {
    *(undefined1 *)(param_1 + 0x41b) = 2;
  }
  if ((*(byte *)(param_1 + 0x41e) < 2) || (99 < *(byte *)(param_1 + 0x41e))) {
    *(undefined1 *)(param_1 + 0x41e) = 5;
  }
  if ((*(byte *)(param_1 + 0x41f) == 0) || (0x14 < *(byte *)(param_1 + 0x41f))) {
    *(undefined1 *)(param_1 + 0x41f) = 2;
  }
  if ((*(uint *)(param_1 + 0x3d8) < 2) || (99 < *(uint *)(param_1 + 0x3d8))) {
    *(undefined4 *)(param_1 + 0x3d8) = 5;
  }
  uVar14 = *(uint *)(param_1 + 0x3dc);
  if ((uVar14 < 2) || (0x14 < uVar14)) {
    iVar6 = 2;
LAB_009a2348:
    *(int *)(param_1 + 0x3dc) = iVar6;
  }
  else if ((uVar14 & 1) != 0) {
    iVar6 = uVar14 - 1;
    goto LAB_009a2348;
  }
  if ((*(uint *)(param_1 + 0x3e0) == 0) || (99 < *(uint *)(param_1 + 0x3e0))) {
    *(undefined4 *)(param_1 + 0x3e0) = 0x32;
  }
  if ((*(uint *)(param_1 + 0x3c8) < 2) || (99 < *(uint *)(param_1 + 0x3c8))) {
    *(undefined4 *)(param_1 + 0x3c8) = 4;
  }
  uVar14 = *(uint *)(param_1 + 0x3cc);
  if ((uVar14 < 2) || (0x14 < uVar14)) {
    iVar6 = 2;
LAB_009a23ac:
    *(int *)(param_1 + 0x3cc) = iVar6;
  }
  else if ((uVar14 & 1) != 0) {
    iVar6 = uVar14 - 1;
    goto LAB_009a23ac;
  }
  if (1 < *(byte *)(param_1 + 0x418)) {
    *(undefined1 *)(param_1 + 0x419) = 1;
  }
  if ((*(uint *)(param_1 + 0x3ec) < 2) || (99 < *(uint *)(param_1 + 0x3ec))) {
    *(undefined4 *)(param_1 + 0x3ec) = 5;
  }
  uVar14 = *(uint *)(param_1 + 0x3f0);
  if ((uVar14 < 2) || (0x14 < uVar14)) {
    iVar6 = 2;
LAB_009a2408:
    *(int *)(param_1 + 0x3f0) = iVar6;
  }
  else if ((uVar14 & 1) != 0) {
    iVar6 = uVar14 - 1;
    goto LAB_009a2408;
  }
  if ((*(uint *)(param_1 + 0x3f4) < 2) || (99 < *(uint *)(param_1 + 0x3f4))) {
    *(undefined4 *)(param_1 + 0x3f4) = 7;
  }
  uVar14 = *(uint *)(param_1 + 0x3f8);
  if ((uVar14 < 2) || (0x14 < uVar14)) {
    iVar6 = 2;
LAB_009a2450:
    *(int *)(param_1 + 0x3f8) = iVar6;
  }
  else if ((uVar14 & 1) != 0) {
    iVar6 = uVar14 - 1;
    goto LAB_009a2450;
  }
  if ((*(byte *)(param_1 + 0x420) < 2) || (99 < *(byte *)(param_1 + 0x420))) {
    *(undefined1 *)(param_1 + 0x420) = 5;
  }
  bVar4 = *(byte *)(param_1 + 0x421);
  if ((bVar4 < 2) || (0x14 < bVar4)) {
    cVar12 = '\x02';
LAB_009a249c:
    *(char *)(param_1 + 0x421) = cVar12;
  }
  else if ((bVar4 & 1) != 0) {
    cVar12 = bVar4 - 1;
    goto LAB_009a249c;
  }
  if ((*(uint *)(param_1 + 0x3d0) < 2) || (99 < *(uint *)(param_1 + 0x3d0))) {
    *(undefined4 *)(param_1 + 0x3d0) = 7;
  }
  uVar14 = *(uint *)(param_1 + 0x3d4);
  if ((uVar14 < 2) || (0x14 < uVar14)) {
    iVar6 = 2;
LAB_009a24e4:
    *(int *)(param_1 + 0x3d4) = iVar6;
  }
  else if ((uVar14 & 1) != 0) {
    iVar6 = uVar14 - 1;
    goto LAB_009a24e4;
  }
  if ((*(uint *)(param_1 + 0x3fc) < 2) || (99 < *(uint *)(param_1 + 0x3fc))) {
    *(undefined4 *)(param_1 + 0x3fc) = 10;
  }
  uVar14 = *(uint *)(param_1 + 0x400);
  if ((uVar14 < 2) || (0x14 < uVar14)) {
    iVar6 = 2;
LAB_009a252c:
    *(int *)(param_1 + 0x400) = iVar6;
  }
  else if ((uVar14 & 1) != 0) {
    iVar6 = uVar14 - 1;
    goto LAB_009a252c;
  }
  if ((*(uint *)(param_1 + 0x3c0) < 4) || (99 < *(uint *)(param_1 + 0x3c0))) {
    *(undefined4 *)(param_1 + 0x3c0) = 7;
  }
  uVar14 = *(uint *)(param_1 + 0x3c4);
  if ((uVar14 < 2) || (0x14 < uVar14)) {
    iVar6 = 2;
LAB_009a2574:
    *(int *)(param_1 + 0x3c4) = iVar6;
  }
  else if ((uVar14 & 1) != 0) {
    iVar6 = uVar14 - 1;
    goto LAB_009a2574;
  }
  if ((*(byte *)(param_1 + 0x419) == 0) || (5 < *(byte *)(param_1 + 0x419))) {
    *(undefined1 *)(param_1 + 0x419) = 3;
  }
  if ((*(byte *)(param_1 + 0x41c) < 2) || (99 < *(byte *)(param_1 + 0x41c))) {
    *(undefined1 *)(param_1 + 0x41c) = 3;
  }
LAB_009a25b0:
  _opd_FUN_0099c6e8(param_1);
  *(undefined2 *)(**(int **)(puVar3 + -0x8000) + 100) = 5;
  return;
}

