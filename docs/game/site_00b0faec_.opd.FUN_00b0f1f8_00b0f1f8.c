// hook SITE 0x00b0faec

void _opd_FUN_00b0f1f8(int param_1)

{
  longlong lVar1;
  bool bVar2;
  bool bVar3;
  float fVar4;
  float fVar5;
  float fVar6;
  float fVar7;
  float fVar8;
  float fVar9;
  ushort uVar10;
  undefined4 uVar11;
  uint uVar12;
  undefined1 auVar13 [32];
  undefined1 auVar14 [32];
  undefined1 auVar15 [32];
  undefined1 auVar16 [32];
  ulonglong *puVar17;
  float fVar18;
  undefined *puVar19;
  undefined1 uVar20;
  ulonglong uVar21;
  int iVar23;
  uint *puVar24;
  int iVar25;
  short sVar33;
  undefined8 uVar22;
  int iVar26;
  uint uVar27;
  undefined4 uVar28;
  undefined *puVar29;
  undefined *puVar30;
  undefined4 *puVar31;
  int iVar32;
  char cVar34;
  short sVar35;
  int iVar36;
  byte bVar37;
  ulonglong unaff_r14;
  ulonglong unaff_r15;
  ulonglong unaff_r16;
  ulonglong unaff_r17;
  ulonglong unaff_r18;
  ulonglong unaff_r19;
  ulonglong unaff_r20;
  ulonglong unaff_r21;
  uint *puVar38;
  ulonglong unaff_r22;
  ulonglong unaff_r23;
  ulonglong unaff_r24;
  ulonglong unaff_r25;
  ulonglong unaff_r26;
  ulonglong unaff_r27;
  ulonglong unaff_r28;
  ulonglong unaff_r29;
  int *piVar39;
  ulonglong unaff_r30;
  ulonglong unaff_r31;
  byte in_cr0;
  byte in_cr1;
  byte in_cr2;
  byte in_cr3;
  byte unaff_cr4;
  byte in_cr5;
  byte in_cr6;
  byte in_cr7;
  ulonglong in_LR;
  double dVar40;
  double dVar41;
  double dVar42;
  double dVar43;
  double dVar44;
  ulonglong uVar45;
  ulonglong uVar46;
  ulonglong in_f28;
  double dVar47;
  ulonglong in_f29;
  ulonglong in_f30;
  ulonglong in_f31;
  double dVar48;
  double dVar49;
  undefined1 auVar50 [16];
  undefined1 auVar51 [16];
  undefined1 auVar52 [16];
  undefined1 auVar53 [16];
  undefined1 auVar54 [16];
  undefined1 local_110 [16];
  
  uVar21 = ZEXT48(&stack0x00000000);
  lVar1 = uVar21 - 0x1d0;
  puVar17 = (ulonglong *)lVar1;
  *puVar17 = uVar21;
  puVar17[0x30] = unaff_r26;
  puVar17[0x32] = unaff_r28;
  puVar17[0x34] = unaff_r30;
  puVar17[0x35] = unaff_r31;
  puVar17[0x36] = in_f28;
  puVar17[0x37] = in_f29;
  puVar17[0x38] = in_f30;
  puVar17[0x39] = in_f31;
  puVar17[0x24] = unaff_r14;
  puVar17[0x25] = unaff_r15;
  puVar17[0x26] = unaff_r16;
  puVar17[0x27] = unaff_r17;
  puVar17[0x28] = unaff_r18;
  puVar17[0x29] = unaff_r19;
  puVar17[0x2a] = unaff_r20;
  puVar17[0x2b] = unaff_r21;
  puVar17[0x2c] = unaff_r22;
  puVar17[0x2d] = unaff_r23;
  puVar17[0x2e] = unaff_r24;
  puVar17[0x2f] = unaff_r25;
  puVar17[0x31] = unaff_r27;
  puVar17[0x33] = unaff_r29;
  puVar19 = PTR_PTR_0121bd10;
  *(uint *)(puVar17 + 0x3b) =
       (uint)(in_cr0 & 0xf) << 0x1c | (uint)(in_cr1 & 0xf) << 0x18 | (uint)(in_cr2 & 0xf) << 0x14 |
       (uint)(in_cr3 & 0xf) << 0x10 | (uint)(unaff_cr4 & 0xf) << 0xc | (uint)(in_cr5 & 0xf) << 8 |
       (uint)(in_cr6 & 0xf) << 4 | (uint)(in_cr7 & 0xf);
  puVar17[0x3c] = in_LR;
  if (*(char *)(param_1 + 0xfe4) == '\0') {
    _opd_FUN_00b10d28(param_1);
    *(undefined1 *)(param_1 + 0xfe4) = 1;
  }
  iVar25 = *(int *)(param_1 + 0x1124);
  iVar23 = _opd_FUN_00bb9fd8();
  if (iVar25 != iVar23) {
    puVar24 = (uint *)_opd_FUN_00242bf0(*(undefined4 *)(param_1 + 0x60),0xd946c2);
    iVar25 = _opd_FUN_00bb9fd8();
    *(int *)(param_1 + 0x1124) = iVar25;
    if (iVar25 == 1) {
      if ((puVar24 != (uint *)0x0) && (*(short *)(puVar24 + 6) != 0xcc)) {
        *(undefined2 *)(puVar24 + 6) = 0xcc;
        *puVar24 = *puVar24 | 0x40000000;
      }
      sVar33 = *(short *)(param_1 + 0x1120) + 0x1d0;
      sVar35 = *(short *)(param_1 + 0x1122) + -0x1d0;
    }
    else {
      if ((puVar24 != (uint *)0x0) && (*(short *)(puVar24 + 6) != 0x100)) {
        *(undefined2 *)(puVar24 + 6) = 0x100;
        *puVar24 = *puVar24 | 0x40000000;
      }
      sVar33 = *(short *)(param_1 + 0x1120);
      sVar35 = *(short *)(param_1 + 0x1122);
    }
    _opd_FUN_00253be0(puVar24,sVar33,sVar35,0,0);
  }
  iVar25 = _opd_FUN_00b1f468();
  if (iVar25 != 0) {
    if (((*(longlong *)(param_1 + 0x1110) == 0) &&
        (iVar25 = _opd_FUN_003087f0(), *(int *)(iVar25 + 8) != 0)) &&
       ((sVar33 = _opd_FUN_00b2f5f8(), sVar33 == 1 || (sVar33 = _opd_FUN_00b2f5f8(), sVar33 == 6))))
    {
      uVar22 = _opd_FUN_00e6c718();
      *(undefined8 *)(param_1 + 0x1110) = uVar22;
      _opd_FUN_000beea8(param_1,uVar22);
      _opd_FUN_00e6a2b0(0);
      puVar31 = (undefined4 *)_opd_FUN_00072aa0();
      uVar28 = 0x5a0;
      if (puVar31 != (undefined4 *)0x0) {
        uVar28 = *puVar31;
      }
      dVar48 = (double)_opd_FUN_00075510(uVar28,*(undefined4 *)(*(int *)(puVar19 + -0x7f60) + 0xb4))
      ;
      fVar4 = (float)((double)*(float *)(puVar19 + -0x7ea8) - dVar48) *
              *(float *)(puVar19 + -0x7f2c) + *(float *)(puVar19 + -0x7e94);
      iVar25 = *(int *)(puVar17 + 0x1c);
      puVar17[0x22] = (longlong)iVar25;
      *(float *)(puVar17 + 0x23) = (float)(longlong)puVar17[0x22] - fVar4;
      if (*(int *)(puVar17 + 0x23) != 0) {
        *(float *)(puVar17 + 0x23) = fVar4;
        if ((*(uint *)(puVar17 + 0x23) & 0x7f800000) < 0x4b800000) {
          if ((int)*(uint *)(puVar17 + 0x23) < 0) {
            puVar17[0x22] = (longlong)(iVar25 + -1);
          }
          iVar25 = *(int *)(puVar17 + 0x1c);
        }
      }
      *(short *)(param_1 + 0x1118) = (short)iVar25;
    }
    if (*(short *)(**(int **)(puVar19 + -0x7f5c) + 0xaca) == 6) {
      _opd_FUN_00e6a2b0(1);
    }
    else {
      _opd_FUN_00e6a2b0(0);
    }
  }
  *(byte *)(param_1 + 100) = *(byte *)(param_1 + 100) | 0x20;
  iVar25 = _opd_FUN_00750108();
  if (((iVar25 == 0) || (iVar25 == 7)) || (sVar33 = _opd_FUN_00b2f5f8(), sVar33 == 4)) {
LAB_00b0f3a0:
    _opd_FUN_00b0e0c8(param_1);
    _opd_FUN_00b0ca98(param_1);
    _opd_FUN_00b0b870(param_1);
    _opd_FUN_00b30830(*(undefined4 *)(param_1 + 0x60),0xd946c2);
    _opd_FUN_002438e0(*(undefined4 *)(param_1 + 0x60));
    if (*(int *)(param_1 + 0x68) != 0) {
      _opd_FUN_002438e0(*(int *)(param_1 + 0x68));
    }
    if (*(int *)(param_1 + 0x6c) != 0) {
      _opd_FUN_002438e0(*(int *)(param_1 + 0x6c));
    }
    return;
  }
  iVar25 = *(int *)(puVar19 + -0x7f60);
  uVar27 = *(uint *)(iVar25 + 0x18) | *(uint *)(iVar25 + 0x1c);
  if ((((uVar27 & 4) != 0) || ((uVar27 & 0x300) != 0)) ||
     ((iVar23 = _opd_FUN_00b19a98(), iVar23 != 0 ||
      ((iVar23 = _opd_FUN_00b1f468(), iVar23 != 0 &&
       (*(short *)(**(int **)(puVar19 + -0x7f5c) + 0xaca) == 6)))))) goto LAB_00b0f3a0;
  _opd_FUN_00b30880(*(undefined4 *)(param_1 + 0x60),0xd946c2);
  _opd_FUN_002438b8(*(undefined4 *)(param_1 + 0x60));
  if (*(int *)(param_1 + 0x68) != 0) {
    _opd_FUN_002438b8(*(int *)(param_1 + 0x68));
  }
  if (*(int *)(param_1 + 0x6c) != 0) {
    _opd_FUN_002438b8(*(int *)(param_1 + 0x6c));
  }
  _opd_FUN_00b0d298(param_1);
  iVar23 = _opd_FUN_00246978(*(undefined4 *)(param_1 + 0x60),0x7e0ee,
                             *(undefined4 *)(param_1 + 0xf70));
  if ((iVar23 == 0) &&
     (iVar23 = _opd_FUN_00246978(*(undefined4 *)(param_1 + 0x60),0x7e0ee,
                                 *(undefined4 *)(param_1 + 0xf84)), iVar23 == 0)) {
    uVar20 = 1;
  }
  else {
    uVar20 = 0;
  }
  iVar23 = 0;
  iVar32 = 0;
  *(undefined1 *)(param_1 + 0xfe5) = uVar20;
  do {
    iVar26 = _opd_FUN_0074fe10(iVar32);
    if (iVar26 != 0) {
      uVar27 = _opd_FUN_00b1df90(iVar26);
      iVar23 = iVar23 - ((int)(((int)uVar27 >> 0x1f) - ((int)uVar27 >> 0x1f ^ uVar27)) >> 0x1f);
    }
    bVar2 = iVar32 != 0x17;
    iVar32 = iVar32 + 1;
  } while (bVar2);
  bVar2 = *(int *)(param_1 + 0xfe8) != iVar23;
  if (bVar2) {
    *(int *)(param_1 + 0xfe8) = iVar23;
  }
  *(bool *)(param_1 + 0xfec) = bVar2;
  auVar51 = auRam00000000 >> 0x60;
  auVar51 = vectorMultiplyAddFloatingPoint
                      (*(undefined1 (*) [16])(puVar17 + 0x14),
                       auVar51 & (undefined1  [16])0xffffffff | auVar51 << 0x20 | auVar51 << 0x40 |
                       auVar51 << 0x60,(undefined1  [16])0x0);
  *(undefined1 (*) [16])(puVar17 + 0x14) = auVar51;
  *(short *)(param_1 + 0xf9c) = (short)-(short)*(undefined4 *)(puVar17 + 0x1c) >> 4;
  if ((*(byte *)(param_1 + 100) & 2) != 0) {
    *(undefined2 *)(param_1 + 0xf9c) = 0;
  }
  puVar24 = *(uint **)(param_1 + 0x7c);
  uVar27 = (uint)*(ushort *)(param_1 + 0xf9c);
  if ((puVar24 != (uint *)0x0) && ((int)*(short *)((int)puVar24 + 0x1a) != (uVar27 & 0xfff))) {
    *(short *)((int)puVar24 + 0x1a) = (short)(uVar27 & 0xfff);
    *puVar24 = *puVar24 | 0x40000000;
    uVar27 = (uint)*(ushort *)(param_1 + 0xf9c);
  }
  puVar24 = *(uint **)(param_1 + 0x74);
  if ((puVar24 != (uint *)0x0) && ((int)*(short *)((int)puVar24 + 0x1a) != (uVar27 & 0xfff))) {
    *(short *)((int)puVar24 + 0x1a) = (short)(uVar27 & 0xfff);
    *puVar24 = *puVar24 | 0x40000000;
    uVar27 = (uint)*(ushort *)(param_1 + 0xf9c);
  }
  puVar24 = *(uint **)(param_1 + 0x80);
  if ((puVar24 != (uint *)0x0) && ((int)*(short *)((int)puVar24 + 0x1a) != (uVar27 & 0xfff))) {
    *(short *)((int)puVar24 + 0x1a) = (short)(uVar27 & 0xfff);
    *puVar24 = *puVar24 | 0x40000000;
  }
  _opd_FUN_00b0e0c8(param_1);
  _opd_FUN_00b0ca98(param_1);
  _opd_FUN_00b0b870(param_1);
  iVar23 = _opd_FUN_00b31810();
  if (((iVar23 == 0) && (iVar23 = _opd_FUN_00b1e088(), iVar23 == 0)) &&
     (iVar23 = _opd_FUN_00b1cbf8(), iVar23 == 0)) {
    iVar23 = _opd_FUN_00b1fcb0();
    iVar32 = _opd_FUN_00b18970();
    if ((iVar32 == 0) || (iVar23 != 3)) {
      iVar32 = _opd_FUN_00b189b0();
      if (iVar32 != 0) {
        iVar32 = _opd_FUN_00b17b68();
        if (((iVar32 == 0) && (iVar32 = _opd_FUN_00b19978(), iVar32 == 0)) &&
           ((iVar23 == 3 &&
            (((iVar23 = _opd_FUN_00b1c9e8(), iVar23 != -1 ||
              (cVar34 = _opd_FUN_00763638(), cVar34 != '\0')) && (0 < *(int *)(param_1 + 0xf30))))))
           ) {
          puVar31 = (undefined4 *)(param_1 + 0xf2c);
          iVar23 = 0;
          do {
            *puVar31 = 0x200002;
            iVar23 = iVar23 + 1;
            puVar31 = puVar31 + 5;
          } while (iVar23 < *(int *)(param_1 + 0xf30));
        }
        uVar28 = _opd_FUN_00790de0(0);
        iVar23 = _opd_FUN_00b1f308(uVar28);
        if (iVar23 == 2) {
          iVar23 = _opd_FUN_00b17310(uVar28);
          uVar28 = 0x516054;
          if ((iVar23 != 0) && (uVar28 = 0x516055, iVar23 != 1)) goto LAB_00b1017c;
        }
        else {
LAB_00b1017c:
          uVar28 = 0xdac8eb;
        }
        puVar31 = (undefined4 *)(param_1 + 0xf1c);
        if (0 < *(int *)(param_1 + 0xf30)) {
          iVar23 = 0;
          do {
            uVar11 = *puVar31;
            iVar23 = iVar23 + 1;
            puVar31 = puVar31 + 5;
            _opd_FUN_00249d40(*(undefined4 *)(param_1 + 0x60),uVar28,uVar11);
          } while (iVar23 < *(int *)(param_1 + 0xf30));
        }
      }
    }
    else {
      iVar23 = _opd_FUN_00b1c1f8();
      if ((iVar23 != 0) && (0 < *(int *)(param_1 + 0x46c))) {
        puVar31 = (undefined4 *)(param_1 + 0x454);
        iVar23 = 0;
        do {
          *puVar31 = 0x102;
          iVar23 = iVar23 + 1;
          puVar31 = puVar31 + 5;
        } while (iVar23 < *(int *)(param_1 + 0x46c));
      }
      iVar23 = _opd_FUN_00b1c198();
      if (iVar23 != 0) {
        *(undefined4 *)(param_1 + 0xf6c) = 0x102;
      }
    }
  }
  _opd_FUN_00b0c350(param_1,param_1 + 0x444,2);
  _opd_FUN_00b0c350(param_1,param_1 + 0xf5c,1);
  _opd_FUN_00b0c350(param_1,param_1 + 0xf70,1);
  _opd_FUN_00b0c350(param_1,param_1 + 0xf84,1);
  _opd_FUN_00b0c350(param_1,param_1 + 0x470,2);
  _opd_FUN_00b0c350(param_1,param_1 + 0x49c,0x10);
  _opd_FUN_00b0c350(param_1,param_1 + 0x620,*(undefined4 *)(param_1 + 0x6ac));
  _opd_FUN_00b0c350(param_1,param_1 + 0x774,0x18);
  _opd_FUN_00b0c350(param_1,param_1 + 0x6b0,8);
  _opd_FUN_00b0c350(param_1,param_1 + 0xf00,1);
  if (*(int *)(param_1 + 0x6c) != 0) {
    _opd_FUN_00b0c350(param_1,param_1 + 0x958,0x18);
  }
  if (*(int *)(param_1 + 0x68) != 0) {
    _opd_FUN_00b0c350(param_1,param_1 + 0xb3c,0x30);
  }
  piVar39 = (int *)(param_1 + 0x5e0);
  _opd_FUN_00b0c350(param_1,param_1 + 0xf1c,1);
  if (((*(uint *)(param_1 + 0x5f0) & 1) == 0) || (iVar23 = _opd_FUN_00b1cab8(), iVar23 != 0)) {
    puVar24 = (uint *)*piVar39;
    if ((puVar24 != (uint *)0x0) && ((*puVar24 & 0x400000) != 0)) {
      *puVar24 = *puVar24 & 0xffbfffff | 0x40000000;
    }
  }
  else {
    _opd_FUN_00253998(*piVar39,0xffffffffffffff80);
    puVar24 = (uint *)*piVar39;
    if ((puVar24 != (uint *)0x0) && ((*puVar24 & 0x400000) == 0)) {
      *puVar24 = *puVar24 | 0x40400000;
    }
  }
  piVar39 = (int *)(param_1 + 0x5f4);
  if (((*(uint *)(param_1 + 0x604) & 1) == 0) || (iVar23 = _opd_FUN_00b1cab8(), iVar23 != 0)) {
    puVar24 = (uint *)*piVar39;
    if ((puVar24 != (uint *)0x0) && ((*puVar24 & 0x400000) != 0)) {
      *puVar24 = *puVar24 & 0xffbfffff | 0x40000000;
    }
  }
  else {
    _opd_FUN_00253998(*piVar39,0xffffffffffffff80);
    puVar24 = (uint *)*piVar39;
    if ((puVar24 != (uint *)0x0) && ((*puVar24 & 0x400000) == 0)) {
      *puVar24 = *puVar24 | 0x40400000;
    }
  }
  piVar39 = (int *)(param_1 + 0x608);
  if (((*(uint *)(param_1 + 0x618) & 1) == 0) || (iVar23 = _opd_FUN_00b1cab8(), iVar23 != 0)) {
    puVar24 = (uint *)*piVar39;
    if ((puVar24 != (uint *)0x0) && ((*puVar24 & 0x400000) != 0)) {
      *puVar24 = *puVar24 & 0xffbfffff | 0x40000000;
    }
  }
  else {
    _opd_FUN_00253998(*piVar39,0xffffffffffffff80);
    puVar24 = (uint *)*piVar39;
    if ((puVar24 != (uint *)0x0) && ((*puVar24 & 0x400000) == 0)) {
      *puVar24 = *puVar24 | 0x40400000;
    }
  }
  _opd_FUN_00253998(*(undefined4 *)(param_1 + 0xf98),-*(short *)(param_1 + 0xf9c));
  uVar28 = _opd_FUN_00036350();
  puVar29 = (undefined *)_opd_FUN_000cdc90(uVar28);
  uVar28 = _opd_FUN_00036350();
  puVar30 = (undefined *)_opd_FUN_000cdc90(uVar28);
  if (puVar30 == &UNK_00f8cac7) {
    _opd_FUN_00b30880(*(undefined4 *)(param_1 + 0x60),0x6a76fd);
  }
  else {
    _opd_FUN_00b30830(*(undefined4 *)(param_1 + 0x60),0x6a76fd);
  }
  puVar24 = (uint *)_opd_FUN_00242bf0(*(undefined4 *)(param_1 + 0x60),&DAT_003abeb2);
  if ((puVar24 != (uint *)0x0) &&
     (uVar27 = *(ushort *)(param_1 + 0xf9c) & 0xfff, (int)*(short *)((int)puVar24 + 0x1a) != uVar27)
     ) {
    *(short *)((int)puVar24 + 0x1a) = (short)uVar27;
    *puVar24 = *puVar24 | 0x40000000;
  }
  if (puVar29 == &UNK_00f8c727) {
    dVar40 = (double)*(float *)(puVar19 + -0x7d90);
    dVar48 = (double)*(float *)(puVar19 + -0x7d50);
    dVar41 = (double)*(float *)(puVar19 + -0x7d48);
    dVar49 = (double)*(float *)(puVar19 + -0x7c48);
    dVar47 = (double)*(float *)(puVar19 + -0x7c60);
    dVar42 = (double)*(float *)(puVar19 + -0x7c58);
    dVar44 = (double)*(float *)(puVar19 + -0x7c50);
    dVar43 = (double)*(float *)(puVar19 + -0x7f98);
    *(undefined4 *)(param_1 + 0x1128) = 0x16a8;
    *(undefined4 *)(param_1 + 0x112c) = 0xffffefe8;
  }
  else if ((int)puVar29 < 0xf8c728) {
    if (puVar29 == &UNK_00f8c687) {
      fVar4 = *(float *)(puVar19 + -0x7e80);
      dVar48 = (double)*(float *)(puVar19 + -0x7cc0);
      dVar41 = (double)*(float *)(puVar19 + -0x7e20);
      dVar49 = (double)*(float *)(puVar19 + -0x7e00);
      dVar47 = (double)*(float *)(puVar19 + -0x7df8);
      dVar42 = (double)*(float *)(puVar19 + -0x7df0);
      dVar44 = (double)*(float *)(puVar19 + -0x7de8);
      dVar43 = (double)*(float *)(puVar19 + -0x7f98);
      *(undefined4 *)(param_1 + 0x1128) = 0x2146;
      *(undefined4 *)(param_1 + 0x112c) = 0xfffffea2;
      dVar40 = (double)fVar4;
    }
    else if ((int)puVar29 < 0xf8c688) {
      if (puVar29 == (undefined *)0x494ec7) {
LAB_00b109ac:
        fVar4 = *(float *)(puVar19 + -0x7e58);
        dVar48 = (double)*(float *)(puVar19 + -0x7e50);
        dVar41 = (double)*(float *)(puVar19 + -0x7e48);
        dVar49 = (double)*(float *)(puVar19 + -0x7cd0);
        dVar47 = (double)*(float *)(puVar19 + -0x7e40);
        dVar42 = (double)*(float *)(puVar19 + -0x7e38);
        dVar44 = (double)*(float *)(puVar19 + -0x7e30);
        dVar43 = (double)*(float *)(puVar19 + -0x7f98);
        *(undefined4 *)(param_1 + 0x1128) = 0x2260;
        *(undefined4 *)(param_1 + 0x112c) = 0x2a3;
        dVar40 = (double)fVar4;
      }
      else if (puVar29 == (undefined *)0x494f07) {
LAB_00b10a58:
        fVar4 = *(float *)(puVar19 + -0x7e80);
        dVar48 = (double)*(float *)(puVar19 + -0x7e28);
        dVar41 = (double)*(float *)(puVar19 + -0x7e20);
        dVar49 = (double)*(float *)(puVar19 + -0x7cc8);
        dVar47 = (double)*(float *)(puVar19 + -0x7e18);
        dVar42 = (double)*(float *)(puVar19 + -0x7e10);
        dVar44 = (double)*(float *)(puVar19 + -0x7e08);
        dVar43 = (double)*(float *)(puVar19 + -0x7f98);
        *(undefined4 *)(param_1 + 0x1128) = 0x1720;
        *(undefined4 *)(param_1 + 0x112c) = 0xffffef98;
        dVar40 = (double)fVar4;
      }
      else {
        if (puVar29 != (undefined *)0x494b28) goto LAB_00b0fba4;
LAB_00b10848:
        fVar4 = *(float *)(puVar19 + -0x7e88);
        dVar48 = (double)*(float *)(puVar19 + -0x7e80);
        dVar41 = (double)*(float *)(puVar19 + -0x7cd8);
        dVar49 = (double)*(float *)(puVar19 + -0x7e78);
        dVar47 = (double)*(float *)(puVar19 + -0x7e70);
        dVar42 = (double)*(float *)(puVar19 + -0x7e68);
        dVar44 = (double)*(float *)(puVar19 + -0x7e60);
        dVar43 = (double)*(float *)(puVar19 + -0x7f98);
        *(undefined4 *)(param_1 + 0x1128) = 0x133d;
        *(undefined4 *)(param_1 + 0x112c) = 0xfffffe02;
        dVar40 = (double)fVar4;
      }
    }
    else {
      if (puVar29 != &UNK_00f8c6c7) {
        if ((int)puVar29 < 0xf8c6c8) {
          if (puVar29 == &UNK_00f8c6a7) goto LAB_00b10848;
        }
        else {
          if (puVar29 == &UNK_00f8c6e7) goto LAB_00b109ac;
          if (puVar29 == &UNK_00f8c707) goto LAB_00b10a58;
        }
        goto LAB_00b0fba4;
      }
      uVar28 = 0x1c1;
      fVar18 = *(float *)(param_1 + 4000) - *(float *)(puVar19 + -0x7de0);
      fVar7 = *(float *)(puVar19 + -0x7dd8);
      fVar4 = *(float *)(puVar19 + -0x7dd0);
      fVar5 = *(float *)(puVar19 + -0x7dc8);
      fVar6 = *(float *)(puVar19 + -0x7dc0);
      fVar8 = *(float *)(puVar19 + -0x7cd0);
      fVar9 = *(float *)(puVar19 + -0x7cd8);
      *(undefined4 *)(param_1 + 0x1128) = 0x4123;
LAB_00b10b74:
      dVar49 = (double)fVar9;
      dVar48 = (double)fVar6;
      dVar41 = (double)fVar8;
      if ((double)((ulonglong)(double)fVar18 | 0x8000000000000000) < 0.0) {
        fVar7 = fVar4;
      }
      dVar47 = (double)fVar7;
      fVar4 = *(float *)(puVar19 + -0x7cb8);
      dVar43 = (double)*(float *)(puVar19 + -0x7f98);
      *(undefined4 *)(param_1 + 0x112c) = uVar28;
      dVar42 = (double)(float)(dVar47 * (double)fVar4);
      dVar44 = (double)(float)(dVar42 + dVar43);
      dVar40 = (double)fVar5;
    }
  }
  else if (puVar29 == &UNK_00f8caa7) {
    dVar40 = (double)*(float *)(puVar19 + -0x7c98);
    dVar48 = (double)*(float *)(puVar19 + -0x7ea8);
    dVar41 = (double)*(float *)(puVar19 + -0x7da0);
    dVar49 = (double)*(float *)(puVar19 + -0x7d98);
    dVar47 = (double)*(float *)(puVar19 + -0x7e70);
    dVar42 = (double)*(float *)(puVar19 + -0x7e68);
    dVar44 = (double)*(float *)(puVar19 + -0x7e60);
    dVar43 = (double)*(float *)(puVar19 + -0x7f98);
    *(undefined4 *)(param_1 + 0x1128) = 0x2aad;
    *(undefined4 *)(param_1 + 0x112c) = 0xfffffe70;
  }
  else if ((int)puVar29 < 0xf8caa8) {
    if (puVar29 == &UNK_00f8c767) {
      dVar40 = (double)*(float *)(puVar19 + -0x7d90);
      dVar48 = (double)*(float *)(puVar19 + -0x7e58);
      dVar41 = (double)*(float *)(puVar19 + -0x7d88);
      dVar49 = (double)*(float *)(puVar19 + -0x7e78);
      dVar47 = (double)*(float *)(puVar19 + -0x7e70);
      dVar42 = (double)*(float *)(puVar19 + -0x7e68);
      dVar44 = (double)*(float *)(puVar19 + -0x7e60);
      dVar43 = (double)*(float *)(puVar19 + -0x7f98);
      *(undefined4 *)(param_1 + 0x1128) = 0x1a90;
      *(undefined4 *)(param_1 + 0x112c) = 0xfffff448;
    }
    else if ((int)puVar29 < 0xf8c768) {
      if (puVar29 == &UNK_00f8c747) {
        fVar4 = *(float *)(puVar19 + -0x7de0);
        fVar5 = *(float *)(puVar19 + -0x7e18);
        fVar8 = *(float *)(puVar19 + -0x7ca8);
        fVar6 = *(float *)(puVar19 + -0x7cb8);
        dVar48 = (double)*(float *)(puVar19 + -0x7db8);
        dVar41 = (double)*(float *)(puVar19 + -0x7db0);
        dVar49 = (double)*(float *)(puVar19 + -0x7cb0);
        *(undefined4 *)(param_1 + 0x1128) = 0x169e;
        *(undefined4 *)(param_1 + 0x112c) = 0xffffe7c8;
        if ((double)((ulonglong)(double)(*(float *)(param_1 + 4000) - fVar4) | 0x8000000000000000) <
            0.0) {
          fVar8 = fVar5;
        }
        dVar47 = (double)fVar8;
        dVar43 = (double)*(float *)(puVar19 + -0x7f98);
        dVar42 = (double)(float)(dVar47 * (double)fVar6);
        dVar44 = (double)(float)(dVar42 + dVar43);
        dVar40 = dVar48;
      }
      else {
LAB_00b0fba4:
        dVar41 = (double)*(float *)(puVar19 + -0x7f90);
        dVar43 = (double)*(float *)(puVar19 + -0x7f98);
        dVar42 = (double)*(float *)(puVar19 + -0x7ce0);
        dVar48 = dVar41;
        dVar40 = dVar41;
        dVar44 = dVar43;
        dVar47 = dVar41;
        dVar49 = dVar41;
      }
    }
    else if (puVar29 == &UNK_00f8c787) {
      dVar40 = (double)*(float *)(puVar19 + -0x7ea8);
      dVar48 = (double)*(float *)(puVar19 + -0x7d70);
      dVar41 = (double)*(float *)(puVar19 + -0x7f90);
      dVar49 = (double)*(float *)(puVar19 + -0x7f50);
      dVar47 = (double)*(float *)(puVar19 + -0x7d68);
      dVar42 = (double)*(float *)(puVar19 + -0x7d60);
      dVar44 = (double)*(float *)(puVar19 + -0x7d58);
      dVar43 = (double)*(float *)(puVar19 + -0x7f98);
      *(undefined4 *)(param_1 + 0x1128) = 13000;
      *(undefined4 *)(param_1 + 0x112c) = 0xffffea84;
    }
    else {
      if (puVar29 != &UNK_00f8ca67) goto LAB_00b0fba4;
      dVar40 = (double)*(float *)(puVar19 + -0x7e80);
      dVar48 = (double)*(float *)(puVar19 + -0x7e88);
      dVar41 = (double)*(float *)(puVar19 + -0x7c70);
      dVar49 = (double)*(float *)(puVar19 + -0x7c68);
      dVar47 = (double)*(float *)(puVar19 + -0x7c60);
      dVar42 = (double)*(float *)(puVar19 + -0x7c58);
      dVar44 = (double)*(float *)(puVar19 + -0x7c50);
      dVar43 = (double)*(float *)(puVar19 + -0x7f98);
      *(undefined4 *)(param_1 + 0x1128) = 0x514;
      *(undefined4 *)(param_1 + 0x112c) = 0xffffd440;
    }
  }
  else if (puVar29 == &UNK_00f8cae7) {
    dVar40 = (double)*(float *)(puVar19 + -0x7e58);
    dVar48 = (double)*(float *)(puVar19 + -0x7d80);
    dVar41 = (double)*(float *)(puVar19 + -0x7c90);
    dVar49 = (double)*(float *)(puVar19 + -0x7d78);
    dVar47 = (double)*(float *)(puVar19 + -0x7c88);
    dVar42 = (double)*(float *)(puVar19 + -0x7c80);
    dVar44 = (double)*(float *)(puVar19 + -0x7c78);
    dVar43 = (double)*(float *)(puVar19 + -0x7f98);
    *(undefined4 *)(param_1 + 0x1128) = 0x2e18;
    *(undefined4 *)(param_1 + 0x112c) = 0x1b8;
  }
  else if ((int)puVar29 < 0xf8cae8) {
    if (puVar29 != &UNK_00f8cac7) goto LAB_00b0fba4;
    dVar40 = (double)*(float *)(puVar19 + -0x7dc0);
    dVar48 = (double)*(float *)(puVar19 + -0x7ca0);
    dVar41 = (double)*(float *)(puVar19 + -0x7f90);
    dVar49 = (double)*(float *)(puVar19 + -0x7da8);
    dVar47 = (double)*(float *)(puVar19 + -0x7e40);
    dVar42 = (double)*(float *)(puVar19 + -0x7e38);
    dVar44 = (double)*(float *)(puVar19 + -0x7e30);
    dVar43 = (double)*(float *)(puVar19 + -0x7f98);
    *(undefined4 *)(param_1 + 0x1128) = 0x2e18;
    *(undefined4 *)(param_1 + 0x112c) = 0x41a;
  }
  else {
    if (puVar29 == (undefined *)0xf8cb07) {
      uVar28 = 0xfffff380;
      fVar18 = *(float *)(param_1 + 4000) - *(float *)(puVar19 + -0x7de0);
      fVar7 = *(float *)(puVar19 + -0x7e70);
      fVar4 = *(float *)(puVar19 + -0x7d40);
      fVar5 = *(float *)(puVar19 + -0x7d38);
      fVar6 = *(float *)(puVar19 + -0x7d30);
      fVar8 = *(float *)(puVar19 + -0x7cd8);
      fVar9 = *(float *)(puVar19 + -0x7d28);
      *(undefined4 *)(param_1 + 0x1128) = 0x12c0;
      goto LAB_00b10b74;
    }
    if (puVar29 != (undefined *)0xf8cb67) goto LAB_00b0fba4;
    dVar40 = (double)*(float *)(puVar19 + -0x7d20);
    dVar48 = (double)*(float *)(puVar19 + -0x7d18);
    dVar41 = (double)*(float *)(puVar19 + -0x7d10);
    dVar49 = (double)*(float *)(puVar19 + -0x7c40);
    dVar47 = (double)*(float *)(puVar19 + -0x7d40);
    dVar42 = (double)*(float *)(puVar19 + -0x7d08);
    dVar44 = (double)*(float *)(puVar19 + -32000);
    dVar43 = (double)*(float *)(puVar19 + -0x7f98);
    *(undefined4 *)(param_1 + 0x1128) = 0xfffff768;
    *(undefined4 *)(param_1 + 0x112c) = 0xffffc3ab;
  }
  fVar4 = *(float *)(param_1 + 0xfd4);
  fVar5 = *(float *)(param_1 + 0xa8);
  fVar6 = *(float *)(param_1 + 0xfc8);
  dVar40 = (double)(float)((double)((float)(dVar41 + (double)(*(float *)(param_1 + 0xa0) -
                                                             *(float *)(param_1 + 0xfc0))) /
                                   (float)(dVar40 * (double)(float)((double)*(float *)(param_1 +
                                                                                      0xfd0) *
                                                                   dVar43))) * dVar43 + dVar43);
  if (puVar29 == &UNK_00f8caa7) {
    dVar40 = (double)(float)(dVar40 + (double)*(float *)(puVar19 + -0x7c38));
  }
  uVar28 = *(undefined4 *)(param_1 + 0x60);
  dVar42 = (double)(float)(dVar40 + dVar42);
  dVar41 = (double)(float)(dVar47 * dVar43);
  *(undefined4 *)(puVar17 + 0x14) = 0x57d678;
  *(undefined4 *)((int)puVar17 + 0xa4) = 0x57d679;
  *(undefined4 *)(puVar17 + 0x15) = 0x57d67a;
  dVar48 = (double)(float)((double)((float)(dVar49 + (double)(fVar5 - fVar6)) /
                                   (float)(dVar48 * (double)(float)((double)fVar4 * dVar43))) *
                           dVar43 + dVar44);
  uVar28 = _opd_FUN_00242bf0(dVar40,uVar28,0x57d678);
  if (puVar29 == &UNK_00f8caa7) {
    _opd_FUN_00b0aa58(dVar42,dVar48,dVar41,dVar47,param_1,uVar28);
    uVar28 = _opd_FUN_00242bf0(*(undefined4 *)(param_1 + 0x60),*(undefined4 *)((int)puVar17 + 0xa4))
    ;
    _opd_FUN_00b0aa58(dVar42,dVar48,dVar41,dVar47,param_1,uVar28);
    uVar28 = _opd_FUN_00242bf0(*(undefined4 *)(param_1 + 0x60),*(undefined4 *)(puVar17 + 0x15));
    _opd_FUN_00b0aa58(dVar42,dVar48,dVar41,dVar47,param_1,uVar28);
  }
  else {
    _opd_FUN_00b0aa58(dVar42,dVar48,dVar47,dVar47,param_1,uVar28);
    uVar28 = _opd_FUN_00242bf0(*(undefined4 *)(param_1 + 0x60),*(undefined4 *)((int)puVar17 + 0xa4))
    ;
    _opd_FUN_00b0aa58(dVar42,dVar48,dVar47,dVar47,param_1,uVar28);
    uVar28 = _opd_FUN_00242bf0(*(undefined4 *)(param_1 + 0x60),*(undefined4 *)(puVar17 + 0x15));
    _opd_FUN_00b0aa58(dVar42,dVar48,dVar47,dVar47,param_1,uVar28);
  }
  iVar23 = _opd_FUN_00b188b0();
  if (iVar23 == 0) {
    bVar37 = *(byte *)(param_1 + 100);
  }
  else {
    piVar39 = *(int **)(puVar19 + -0x7eb4);
    if (((*piVar39 == 0) || (-1 < *(longlong *)(*piVar39 + 0xe0))) ||
       (iVar23 = _opd_FUN_00b1cab8(), iVar23 != 0)) {
      _opd_FUN_00b30830(*(undefined4 *)(param_1 + 0x60),0x57d67b);
      bVar37 = *(byte *)(param_1 + 100);
    }
    else {
      _opd_FUN_00b30880(*(undefined4 *)(param_1 + 0x60),0x57d67b);
      _opd_FUN_00b30b28(*(undefined4 *)(param_1 + 0x60),0x57d67b,0xd26e2850);
      puVar24 = (uint *)_opd_FUN_00242bf0(*(undefined4 *)(param_1 + 0x60),0x57d67b);
      *puVar24 = *puVar24 | 0x40000000;
      _opd_FUN_00b30880(*(undefined4 *)(param_1 + 0x60),0x6a76fd);
      iVar23 = *(ushort *)((int)puVar24 + 0x1a) - 1;
      dVar48 = (double)(*(float *)(*piVar39 + 0xe4) * *(float *)(puVar19 + -0x7e80) *
                       *(float *)(param_1 + 4000));
      if (-1 < iVar23) {
        puVar38 = puVar24 + 7;
        bVar2 = false;
        while( true ) {
          uVar10 = *(ushort *)(puVar38 + 1);
          uVar27 = puVar38[2];
          iVar32 = uVar10 - 1;
          if (-1 < iVar32) {
            dVar40 = (double)*(float *)(puVar19 + -0x7d28);
            iVar26 = 0;
            while( true ) {
              if (bVar2) {
                iVar36 = iVar32 << 4;
                if (((*(byte *)(param_1 + 100) & 0x10) == 0) &&
                   (iVar36 = iVar32 << 4, (int)(((uint)uVar10 - iVar26) + -1) < 0x11)) {
                  iVar36 = iVar32 * 0x10;
                  sVar33 = *(short *)(uVar27 + 8);
                  auVar52 = *(undefined1 (*) [16])(*(uint *)(puVar19 + -0x7cf4) & 0xfffffff0);
                  auVar54 = *(undefined1 (*) [16])(*(uint *)(puVar19 + -0x7cf0) & 0xfffffff0);
                  puVar17[0x22] = (longlong)*(short *)(uVar27 + 10);
                  uVar46 = puVar17[0x22];
                  puVar17[0x22] = (longlong)sVar33;
                  *(undefined4 *)(puVar17 + 0x19) = 0;
                  *(undefined4 *)((int)puVar17 + 0xcc) = 0x3f800000;
                  *(float *)((int)puVar17 + 0xc4) = (float)(longlong)uVar46;
                  *(float *)(puVar17 + 0x18) = (float)(longlong)puVar17[0x22];
                  auVar53 = vectorSubtractFloatingPoint
                                      (local_110,
                                       *(undefined1 (*) [16])(param_1 + 0xff0U & 0xfffffff0));
                  auVar51 = vectorMultiplyAddFloatingPoint(auVar53,auVar53,(undefined1  [16])0x0);
                  auVar15._0_16_ = auVar53;
                  auVar15._16_16_ = auVar53;
                  auVar16._0_16_ = auVar53;
                  auVar16._16_16_ = auVar53;
                  auVar51 = vectorMultiplyAddFloatingPoint(auVar15._4_16_,auVar15._4_16_,auVar51);
                  auVar51 = vectorMultiplyAddFloatingPoint(auVar16._8_16_,auVar16._8_16_,auVar51);
                  auVar51 = vectorPermute(auVar51,auVar51,
                                          *(undefined1 (*) [16])
                                           (*(uint *)(puVar19 + -0x7cf8) & 0xfffffff0));
                  auVar50 = vectorReciprocalSquareRootEstimateFloatingPoint(auVar51);
                  auVar51 = vectorMultiplyAddFloatingPoint(auVar50,auVar51,(undefined1  [16])0x0);
                  auVar52 = vectorMultiplyAddFloatingPoint(auVar50,auVar52,(undefined1  [16])0x0);
                  auVar51 = vectorNegativeMultiplySubtractFloatingPoint(auVar50,auVar51,auVar54);
                  auVar51 = vectorMultiplyAddFloatingPoint(auVar51,auVar52,auVar50);
                  auVar54 = vectorCompareEqualToFloatingPoint(auVar51,auVar51);
                  auVar51 = vectorConditionalSelect(auVar50,auVar51,auVar54);
                  auVar51 = vectorMultiplyAddFloatingPoint(auVar53,auVar51,(undefined1  [16])0x0);
                  *(undefined1 (*) [16])(param_1 + iVar36 + 0x1000 & 0xfffffff0) = auVar51;
                }
                uVar12 = *(uint *)(puVar19 + -0x7cec);
                *(undefined4 *)(puVar17 + 0x1e) = 0;
                auVar50 = *(undefined1 (*) [16])(uVar12 & 0xfffffff0);
                auVar51 = loadVectorElementWordIndexed(lVar1,0xf0);
                *(float *)(puVar17 + 0x20) = (float)dVar48;
                auVar51 = auVar51 >> 0x60;
                auVar54 = loadVectorElementWordIndexed(lVar1,0x100);
                auVar51 = vectorPermute(auVar51 & (undefined1  [16])0xffffffff | auVar51 << 0x20 |
                                        auVar51 << 0x40 | auVar51 << 0x60,auVar54,auVar50);
                auVar51 = auVar51 >> 0x60;
                auVar51 = vectorMultiplyAddFloatingPoint
                                    (*(undefined1 (*) [16])(param_1 + iVar36 + 0x1000 & 0xfffffff0),
                                     auVar51 & (undefined1  [16])0xffffffff | auVar51 << 0x20 |
                                     auVar51 << 0x40 | auVar51 << 0x60,(undefined1  [16])0x0);
                vectorAddFloatingPoint(*(undefined1 (*) [16])(puVar17 + 0x16),auVar51);
                *(undefined1 (*) [16])(puVar17 + 0x18) = auVar51;
                *(short *)(uVar27 + 8) = (short)*(undefined4 *)(puVar17 + 0x1c);
                *(short *)(uVar27 + 10) = (short)*(undefined4 *)(puVar17 + 0x1c);
              }
              else {
                if ((*(byte *)(param_1 + 100) & 0x10) == 0) {
                  sVar33 = *(short *)(uVar27 + 10);
                  puVar17[0x22] = (longlong)*(short *)(uVar27 + 8);
                  uVar46 = puVar17[0x22];
                  puVar17[0x22] = (longlong)sVar33;
                  uVar45 = puVar17[0x22];
                  *(float *)(param_1 + 0xff0) = (float)(longlong)uVar46;
                  *(undefined4 *)(param_1 + 0xffc) = 0x3f800000;
                  *(float *)(param_1 + 0xff4) = (float)(longlong)uVar45;
                  *(undefined4 *)(param_1 + 0xff8) = 0;
                }
                iVar36 = *piVar39;
                bVar2 = true;
                puVar17[0x10] = *(ulonglong *)(iVar36 + 0xd0);
                puVar17[0x11] = *(ulonglong *)(iVar36 + 0xd8);
                puVar17[0x22] = (longlong)(short)*(undefined4 *)(puVar17 + 0x1c);
                uVar46 = puVar17[0x22];
                uVar28 = *(undefined4 *)(puVar17 + 0x1c);
                *(short *)(uVar27 + 8) = (short)*(undefined4 *)(puVar17 + 0x1c);
                *(short *)(uVar27 + 10) = (short)uVar28;
                puVar17[0x22] = (longlong)(short)uVar28;
                *(undefined4 *)(puVar17 + 0x17) = 0;
                *(float *)(puVar17 + 0x16) = (float)(longlong)uVar46;
                *(undefined4 *)((int)puVar17 + 0xbc) = 0x3f800000;
                *(float *)((int)puVar17 + 0xb4) = (float)(longlong)puVar17[0x22];
              }
              local_110 = vectorSubtractFloatingPoint
                                    (*(undefined1 (*) [16])(puVar17 + 0x12),
                                     *(undefined1 (*) [16])(param_1 + 0xff0U & 0xfffffff0));
              if (dVar40 < (double)SQRT(ABS(*(float *)(puVar17 + 0x19) * *(float *)(puVar17 + 0x19)
                                            + *(float *)(puVar17 + 0x18) *
                                              *(float *)(puVar17 + 0x18) +
                                              *(float *)((int)puVar17 + 0xc4) *
                                              *(float *)((int)puVar17 + 0xc4)))) {
                auVar13._0_16_ = local_110;
                auVar13._16_16_ = local_110;
                auVar14._0_16_ = local_110;
                auVar14._16_16_ = local_110;
                auVar50 = vectorMultiplyAddFloatingPoint(local_110,local_110,(undefined1  [16])0x0);
                auVar51 = auRam00000000 >> 0x60;
                auVar50 = vectorMultiplyAddFloatingPoint(auVar13._4_16_,auVar13._4_16_,auVar50);
                auVar50 = vectorMultiplyAddFloatingPoint(auVar14._8_16_,auVar14._8_16_,auVar50);
                auVar50 = vectorPermute(auVar50,auVar50,
                                        *(undefined1 (*) [16])
                                         (*(uint *)(puVar19 + -0x7cf8) & 0xfffffff0));
                auVar54 = vectorReciprocalSquareRootEstimateFloatingPoint(auVar50);
                auVar50 = vectorMultiplyAddFloatingPoint(auVar54,auVar50,(undefined1  [16])0x0);
                auVar52 = vectorMultiplyAddFloatingPoint
                                    (auVar54,*(undefined1 (*) [16])
                                              (*(uint *)(puVar19 + -0x7cf4) & 0xfffffff0),
                                     (undefined1  [16])0x0);
                auVar50 = vectorNegativeMultiplySubtractFloatingPoint
                                    (auVar54,auVar50,
                                     *(undefined1 (*) [16])
                                      (*(uint *)(puVar19 + -0x7cf0) & 0xfffffff0));
                auVar50 = vectorMultiplyAddFloatingPoint(auVar50,auVar52,auVar54);
                auVar52 = vectorCompareEqualToFloatingPoint(auVar50,auVar50);
                auVar50 = vectorConditionalSelect(auVar54,auVar50,auVar52);
                local_110 = vectorMultiplyAddFloatingPoint(local_110,auVar50,(undefined1  [16])0x0);
                auVar51 = vectorMultiplyAddFloatingPoint
                                    (*(undefined1 (*) [16])(puVar17 + 0x18),
                                     auVar51 & (undefined1  [16])0xffffffff | auVar51 << 0x20 |
                                     auVar51 << 0x40 | auVar51 << 0x60,(undefined1  [16])0x0);
                *(undefined1 (*) [16])(puVar17 + 0x18) = auVar51;
                *(short *)(uVar27 + 8) = (short)*(undefined4 *)(puVar17 + 0x1c);
                *(short *)(uVar27 + 10) = (short)*(undefined4 *)(puVar17 + 0x1c);
              }
              iVar26 = iVar26 + 1;
              _opd_FUN_00253c60(puVar24,0xd26e2850,0xf,0,iVar32);
              bVar3 = iVar32 == 0;
              iVar32 = iVar32 + -1;
              if (bVar3) break;
              uVar27 = uVar27 + 0xc;
            }
          }
          bVar3 = iVar23 == 0;
          iVar23 = iVar23 + -1;
          if (bVar3) break;
          puVar38 = puVar38 + 3;
        }
      }
      bVar37 = *(byte *)(param_1 + 100) | 0x10;
      *(byte *)(param_1 + 100) = bVar37;
    }
  }
  if ((bVar37 & 0x20) != 0) {
    iVar23 = *(int *)(param_1 + 0x111c);
    iVar32 = _opd_FUN_000ba670();
    iVar23 = iVar23 - (int)*(undefined8 *)(iVar32 + 0x10);
    *(int *)(param_1 + 0x111c) = iVar23;
    if (iVar23 < 0) {
      *(undefined4 *)(param_1 + 0x111c) = 0;
    }
    else if (iVar23 != 0) goto LAB_00b0fd50;
    _opd_FUN_00b30830(*(undefined4 *)(param_1 + 0x60),0x696217);
    _opd_FUN_00b04ad8(*(undefined4 *)(param_1 + 0x60),0xa7df47);
  }
LAB_00b0fd50:
  _opd_FUN_00b30a98(*(undefined4 *)(param_1 + 0x60),0xd946c2,0x60);
  iVar23 = _opd_FUN_00b1f468();
  if (iVar23 == 0) {
    _opd_FUN_00b30830(*(undefined4 *)(param_1 + 0x60),0x9245d);
    _opd_FUN_00b30830(*(undefined4 *)(param_1 + 0x60),0x8d5e2);
  }
  else {
    _opd_FUN_00b30880(*(undefined4 *)(param_1 + 0x60),0x9245d);
    _opd_FUN_00b30880(*(undefined4 *)(param_1 + 0x60),0x8d5e2);
    puVar31 = (undefined4 *)_opd_FUN_00072aa0();
    uVar28 = 0x5a0;
    if (puVar31 != (undefined4 *)0x0) {
      uVar28 = *puVar31;
    }
    dVar48 = (double)_opd_FUN_00075510(uVar28,*(undefined4 *)(iVar25 + 0xb4));
    fVar4 = (float)((double)*(float *)(puVar19 + -0x7ea8) - dVar48) * *(float *)(puVar19 + -0x7f2c)
            + *(float *)(puVar19 + -0x7e94);
    iVar25 = *(int *)(puVar17 + 0x1c);
    puVar17[0x22] = (longlong)iVar25;
    *(float *)(puVar17 + 0x23) = (float)(longlong)puVar17[0x22] - fVar4;
    if (*(int *)(puVar17 + 0x23) != 0) {
      *(float *)(puVar17 + 0x23) = fVar4;
      if ((*(uint *)(puVar17 + 0x23) & 0x7f800000) < 0x4b800000) {
        if ((int)*(uint *)(puVar17 + 0x23) < 0) {
          puVar17[0x22] = (longlong)(iVar25 + -1);
        }
        iVar25 = *(int *)(puVar17 + 0x1c);
      }
    }
    *(short *)(puVar17 + 0xe) = (short)iVar25;
    _opd_FUN_00171838(param_1 + 0x1118,uVar21 - 0x160);
    sVar33 = *(short *)(param_1 + 0x1118);
    if (sVar33 < 99) {
      if (sVar33 < -99) {
        _opd_FUN_00fa1c08(uVar21 - 0x110,*(undefined4 *)(puVar19 + -0x7ce4),0xffffffffffffff9c);
      }
      else {
        _opd_FUN_00fa1c08(uVar21 - 0x110,*(undefined4 *)(puVar19 + -0x7ce4),sVar33);
      }
    }
    else {
      _opd_FUN_00fa1c08(uVar21 - 0x110,*(undefined4 *)(puVar19 + -0x7ce4),99);
    }
    uVar21 = uVar21 - 0x110 & 0xffffffff;
    _opd_FUN_00b310a0(*(undefined4 *)(param_1 + 0x60),0x9245d,uVar21,0xf7a368);
    _opd_FUN_00b310a0(*(undefined4 *)(param_1 + 0x60),0x8d5e2,uVar21,0);
  }
  _opd_FUN_0024d078((double)*(float *)(puVar19 + -0x7ea8),*(undefined4 *)(param_1 + 0x60));
  if (*(int *)(param_1 + 0x68) != 0) {
    _opd_FUN_0024d078((double)*(float *)(puVar19 + -0x7ea8),*(int *)(param_1 + 0x68));
  }
  if (*(int *)(param_1 + 0x6c) == 0) {
    return;
  }
  _opd_FUN_0024d078((double)*(float *)(puVar19 + -0x7ea8),*(int *)(param_1 + 0x6c));
  return;
}

