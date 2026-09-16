// original call TARGET 0x002666c8

undefined4 _opd_FUN_002666c8(int *param_1,ulonglong param_2,ulonglong param_3)

{
  bool bVar1;
  undefined2 uVar2;
  undefined2 uVar3;
  undefined1 *puVar4;
  byte *pbVar5;
  byte *pbVar6;
  byte *pbVar7;
  byte *pbVar8;
  ulonglong uVar9;
  byte *pbVar10;
  bool bVar11;
  ulonglong uVar12;
  short sVar15;
  int *piVar14;
  ulonglong uVar13;
  byte bVar16;
  ulonglong uVar17;
  int iVar18;
  uint *puVar19;
  int iVar20;
  uint uVar21;
  int iVar22;
  uint *puVar23;
  char cVar28;
  char cVar29;
  int iVar24;
  char cVar30;
  ushort uVar27;
  undefined4 uVar25;
  byte *pbVar26;
  uint uVar31;
  longlong lVar32;
  ulonglong uVar33;
  uint uVar35;
  uint uVar36;
  longlong lVar34;
  int *piVar37;
  ulonglong uVar38;
  ulonglong uVar39;
  byte bVar40;
  ulonglong uVar41;
  ulonglong uVar42;
  byte in_cr0;
  byte in_cr1;
  byte in_cr2;
  byte in_cr3;
  byte unaff_cr4;
  byte in_cr5;
  byte in_cr6;
  byte in_cr7;
  longlong lVar43;
  uint uStack00000008;
  uint local_29f0;
  uint local_29ec;
  byte local_2148;
  byte local_2147;
  byte local_2146;
  byte local_2145;
  byte local_2144;
  byte local_2143;
  byte local_2142;
  byte local_2141;
  byte local_2140;
  byte local_213f;
  byte local_213e;
  byte local_213d;
  byte local_213c;
  byte local_213b;
  byte local_213a;
  byte local_2139;
  int local_140;
  int local_13c;
  ushort local_122;
  int local_11c;
  ulonglong local_98;
  
  iVar20 = (int)param_3;
  uVar17 = ZEXT48(&stack0x00000000);
  uStack00000008 =
       (uint)(in_cr0 & 0xf) << 0x1c | (uint)(in_cr1 & 0xf) << 0x18 | (uint)(in_cr2 & 0xf) << 0x14 |
       (uint)(in_cr3 & 0xf) << 0x10 | (uint)(unaff_cr4 & 0xf) << 0xc | (uint)(in_cr5 & 0xf) << 8 |
       (uint)(in_cr6 & 0xf) << 4 | (uint)(in_cr7 & 0xf);
  uVar12 = param_2;
  if ((*(ushort *)(param_1 + 5) & 4) == 0) {
    if (0x80 < (param_3 & 0xffffffff)) {
      return 0;
    }
    uVar12 = uVar17 - 0x29cc;
    _opd_FUN_00f9ac38(uVar12,param_2 & 0xffffffff,param_3 & 0xffffffff);
  }
  uVar33 = param_3 - 2;
  pbVar5 = (byte *)uVar12;
  pbVar26 = pbVar5 + 1;
  uVar9 = uVar12 & 0xffffffff;
  uVar35 = iVar20 * 0x5d588b65 + 1;
  uVar31 = uVar35 >> 0x10;
  uVar21 = uVar35 * 0x5d588b65 + 1;
  uVar35 = uVar21 >> 0x10;
  uVar36 = uVar21 * 0x5d588b65 + 1;
  uVar21 = uVar36 >> 0x10;
  uVar39 = (longlong)(int)uVar36 * 0x5d588b65 + 1U & 0xffffffff;
  bVar40 = pbVar5[(int)(uVar39 >> 0x10) - (int)((uVar39 >> 0x10) / (param_3 & 0xffffffff)) * iVar20]
  ;
  pbVar5[(int)(uVar39 >> 0x10) - (int)((uVar39 >> 0x10) / (param_3 & 0xffffffff)) * iVar20] =
       *pbVar26;
  *pbVar26 = bVar40;
  bVar40 = pbVar5[uVar21 - (int)((ulonglong)uVar21 / (param_3 & 0xffffffff)) * iVar20];
  pbVar5[uVar21 - (int)((ulonglong)uVar21 / (param_3 & 0xffffffff)) * iVar20] = *pbVar5;
  *pbVar5 = bVar40;
  *pbVar26 = *pbVar26 ^
             pbVar5[(uVar35 - (int)((ulonglong)uVar35 / (uVar33 & 0xffffffff)) * (int)uVar33) + 2];
  bVar40 = *pbVar5;
  bVar16 = pbVar5[(uVar31 - (int)((ulonglong)uVar31 / (uVar33 & 0xffffffff)) * (int)uVar33) + 2];
  *pbVar5 = bVar40 ^ bVar16;
  local_122 = CONCAT11(pbVar5[1],bVar40 ^ bVar16);
  if (*(char *)((int)param_1 + 5) == '\x02') {
    bVar1 = false;
    uVar27 = *(ushort *)(param_1 + 5);
  }
  else {
    uVar27 = local_122 - *(short *)((int)param_1 + 0x42);
    bVar1 = ((int)(short)uVar27 - 0x4000U & 0x8000) == 0;
    if (bVar1) {
      uVar27 = uVar27 ^ 0x8000;
    }
    if ((0x100 < uVar27) ||
       (((short)uVar27 < 0x20 && ((1 << ((int)(short)uVar27 & 0x3fU) & param_1[0x11]) != 0))))
    goto LAB_00266918;
    uVar27 = *(ushort *)(param_1 + 5);
  }
  if ((uVar27 & 8) == 0) {
    uVar39 = uVar17 - 0x29e8 & 0xffffffff;
    uVar33 = uVar17 - 0x2948 & 0xffffffff;
    local_29f0 = 0x2b58de69;
    _opd_FUN_0026ca98(uVar39,uVar17 - 0x29f0,4);
    uVar38 = param_3 - 10 & 0xffffffff;
    local_13c = (int)(param_3 - 10);
    _opd_FUN_00f99b00(uVar33);
    uVar41 = uVar17 - 0x2148 & 0xffffffff;
    _opd_FUN_00f99b60(uVar33,uVar39,4);
    uVar13 = uVar17 - 0x29d8;
    _opd_FUN_00f99b60(uVar33,uVar9,uVar38);
    _opd_FUN_00f99d28(uVar33,uVar41);
    _opd_FUN_00f99b00(uVar33);
    _opd_FUN_00f99b60(uVar33,uVar39,4);
    uVar31 = 0x87103c2f;
    _opd_FUN_00f99b60(uVar33,uVar41,0x10);
    _opd_FUN_00f99d28(uVar33,uVar41);
    pbVar10 = (byte *)uVar13;
    pbVar26 = pbVar10 + 1;
    pbVar5 = pbVar10 + 2;
    *pbVar10 = local_2148;
    *pbVar26 = local_2147;
    uVar42 = (uVar12 + param_3) - 10 & 0xffffffff;
    pbVar7 = pbVar10 + 4;
    pbVar6 = pbVar10 + 3;
    pbVar8 = pbVar10 + 5;
    *pbVar10 = *pbVar10 ^ local_213e;
    *pbVar26 = *pbVar26 ^ local_213d;
    *pbVar5 = *pbVar5 ^ local_213c;
    *pbVar6 = *pbVar6 ^ local_213b;
    *pbVar7 = *pbVar7 ^ local_213a;
    *pbVar8 = *pbVar8 ^ local_2139;
    iVar18 = _opd_FUN_00fa4e20(uVar13 & 0xffffffff,uVar42,10);
    if (iVar18 != 0) {
      local_29ec = param_1[3] ^ param_1[4];
      _opd_FUN_0026ca98(uVar39,uVar17 - 0x29ec,4);
      _opd_FUN_00f99b00(uVar33);
      _opd_FUN_00f99b60(uVar33,uVar39,4);
      _opd_FUN_00f99b60(uVar33,uVar9,uVar38);
      _opd_FUN_00f99d28(uVar33,uVar41);
      _opd_FUN_00f99b00(uVar33);
      _opd_FUN_00f99b60(uVar33,uVar39,4);
      _opd_FUN_00f99b60(uVar33,uVar41,0x10);
      _opd_FUN_00f99d28(uVar33,uVar41);
      *pbVar10 = local_2148;
      *pbVar26 = local_2147;
      *pbVar10 = *pbVar10 ^ local_213e;
      *pbVar26 = *pbVar26 ^ local_213d;
      *pbVar5 = *pbVar5 ^ local_213c;
      *pbVar6 = *pbVar6 ^ local_213b;
      *pbVar7 = *pbVar7 ^ local_213a;
      *pbVar8 = *pbVar8 ^ local_2139;
      iVar18 = _opd_FUN_00fa4e20(uVar13 & 0xffffffff,uVar42,10);
      if (iVar18 != 0) goto LAB_00266918;
      uVar31 = param_1[2] ^ param_1[4];
      *(ushort *)(param_1 + 5) = *(ushort *)(param_1 + 5) | 9;
    }
  }
  else {
    uVar39 = uVar17 - 0x29e8 & 0xffffffff;
    local_29ec = param_1[3] ^ param_1[4];
    uVar33 = uVar17 - 0x2948 & 0xffffffff;
    _opd_FUN_0026ca98(uVar39,uVar17 - 0x29ec,4);
    local_13c = iVar20 + -10;
    _opd_FUN_00f99b00(uVar33);
    uVar38 = uVar17 - 0x2148 & 0xffffffff;
    _opd_FUN_00f99b60(uVar33,uVar39,4);
    _opd_FUN_00f99b60(uVar33,uVar9,local_13c);
    _opd_FUN_00f99d28(uVar33,uVar38);
    _opd_FUN_00f99b00(uVar33);
    _opd_FUN_00f99b60(uVar33,uVar39,4);
    _opd_FUN_00f99b60(uVar33,uVar38,0x10);
    _opd_FUN_00f99d28(uVar33,uVar38);
    pbVar5 = (byte *)(uVar17 - 0x29d8);
    pbVar26 = pbVar5 + 1;
    *pbVar5 = local_2148;
    *pbVar26 = local_2147;
    pbVar5[2] = local_2146;
    pbVar5[3] = local_2145;
    pbVar5[4] = local_2144;
    pbVar5[5] = local_2143;
    pbVar5[6] = local_2142;
    pbVar5[7] = local_2141;
    pbVar5[8] = local_2140;
    pbVar5[9] = local_213f;
    *pbVar5 = *pbVar5 ^ local_213e;
    *pbVar26 = *pbVar26 ^ local_213d;
    pbVar5[2] = pbVar5[2] ^ local_213c;
    pbVar5[3] = pbVar5[3] ^ local_213b;
    pbVar5[4] = pbVar5[4] ^ local_213a;
    pbVar5[5] = pbVar5[5] ^ local_2139;
    iVar18 = _opd_FUN_00fa4e20(uVar17 - 0x29d8 & 0xffffffff,(uVar12 + param_3) - 10 & 0xffffffff,10)
    ;
    if (iVar18 != 0) goto LAB_00266918;
    if (param_1[0x12] != -1) {
      _opd_FUN_0026b1c8(param_1 + 0x12);
    }
    uVar31 = param_1[2] ^ param_1[4];
  }
  local_98 = uVar17 - 0x29d8;
  uVar33 = uVar17 - 0x29e8;
  uVar39 = uVar17 - 0x2148;
  *(undefined2 *)((int)param_1 + 6) = 0;
  if (bVar1) {
    local_122 = local_122 ^ 0x8000;
  }
  local_13c = local_13c + -2;
  uVar12 = uVar12 + 2;
  _opd_FUN_0026ca98(uVar17 - 0x29f0,uVar9,2);
  if (0 < local_13c) {
    uVar38 = uVar17 - 0x29ec & 0xffffffff;
    uVar41 = uVar12 & 0xffffffff;
    uVar31 = (local_29f0 >> 0x10 & 0x7fff) * 0x5d588b65 + 1 ^ uVar31;
    _opd_FUN_0026ca98(uVar38,uVar41,4);
    local_29ec = local_29ec ^ uVar31;
    uVar42 = uVar12;
    iVar18 = local_13c;
    if (3 < local_13c) goto LAB_00266c90;
    do {
      iVar22 = 0;
      lVar34 = (ulonglong)(iVar18 - 1) + 1;
      if (iVar18 < 1) {
        lVar34 = 1;
      }
      do {
        puVar4 = (undefined1 *)(iVar22 + (int)uVar42);
        iVar22 = iVar22 + 1;
        *puVar4 = (char)local_29ec;
        local_29ec = local_29ec >> 8;
        lVar34 = lVar34 + -1;
      } while (lVar34 != 0);
      while( true ) {
        iVar18 = iVar18 + -4;
        if (iVar18 < 1) goto LAB_00266cb4;
        uVar42 = uVar42 + 4;
        uVar41 = uVar42 & 0xffffffff;
        uVar31 = uVar31 + local_29ec;
        _opd_FUN_0026ca98(uVar38,uVar41,4);
        local_29ec = local_29ec ^ uVar31;
        if (iVar18 < 4) break;
LAB_00266c90:
        _opd_FUN_0026ca98(uVar41,uVar38,4);
      }
    } while( true );
  }
LAB_00266cb4:
  if (bVar1) {
    iVar18 = _opd_FUN_00efd840(uVar39,0x2000,uVar12 & 0xffffffff,local_13c,uVar17 - 0x2948,0x800,0,0
                              );
    if (iVar18 < 1) {
LAB_00266918:
      *(short *)((int)param_1 + 6) = *(short *)((int)param_1 + 6) + 1;
      return 0;
    }
    iVar20 = iVar18 + 0xc;
    _opd_FUN_00f9ac38(uVar12 & 0xffffffff,uVar17 - 0x2948,iVar18);
  }
  if (*(char *)((int)param_1 + 5) == '\x02') {
    uVar31 = 1;
    *(ushort *)((int)param_1 + 0x42) = local_122 + 1;
    goto LAB_00266d48;
  }
  if ((*(ushort *)(param_1 + 5) & 4) == 0) {
    _opd_FUN_00269390(uVar17 - 0x29d8,uVar9,iVar20);
    while (iVar18 = _opd_FUN_002693a8(local_98 & 0xffffffff), iVar18 != 0) {
      uVar12 = _opd_FUN_002692c0(iVar18);
      if ((((uVar12 & 0xfff) == 0) && ((uVar12 & 0x4000) == 0)) &&
         (uVar27 = _opd_FUN_002692e0(iVar18), 3 < uVar27)) {
        uVar25 = _opd_FUN_00269320(iVar18);
        _opd_FUN_0026ca98(uVar17 - 0x29ec & 0xffffffff,uVar25,4);
        if (param_1[6] != local_29ec) {
          return 0;
        }
      }
      _opd_FUN_002693e0(local_98 & 0xffffffff);
    }
  }
  uVar12 = (ulonglong)*(ushort *)((int)param_1 + 0x42);
  sVar15 = local_122 - *(ushort *)((int)param_1 + 0x42);
  if (sVar15 < 0x20) {
    uVar35 = (uint)param_1[0x11] >> ((int)sVar15 & 0x3fU);
    uVar21 = param_1[0x11] | 1 << ((int)sVar15 & 0x3fU);
    uVar38 = (ulonglong)uVar21;
    uVar31 = (int)uVar35 >> 0x1f;
    param_1[0x11] = uVar21;
    uVar31 = ((uVar31 ^ uVar35) - uVar31) - 1 >> 0x1f;
    if ((uVar21 & 1) != 0) {
      do {
        uVar38 = uVar38 >> 1;
        uVar12 = uVar12 + 1;
      } while ((uVar38 & 1) != 0);
      param_1[0x11] = (int)uVar38;
      *(short *)((int)param_1 + 0x42) = (short)uVar12;
    }
    goto LAB_00266d48;
  }
  lVar34 = (longlong)sVar15 + -0x1f;
  uVar31 = (uint)lVar34;
  if ((int)uVar31 < 0x21) {
    lVar32 = 0;
    iVar18 = 0;
    if (0 < (int)uVar31) goto LAB_00267a3c;
    uVar35 = param_1[0x11];
  }
  else {
    lVar32 = (longlong)sVar15 + -0x3f;
LAB_00267a3c:
    uVar35 = param_1[0x11];
    uVar21 = 0;
    lVar43 = 0x20;
    do {
      uVar36 = uVar21 & 0x3f;
      uVar21 = uVar21 + 1;
      uVar36 = 1 << uVar36 & uVar35;
      uVar38 = (ulonglong)((int)uVar36 >> 0x1f);
      lVar32 = lVar32 + ((((uVar38 ^ uVar36) - uVar38) - 1 & 0xffffffff) >> 0x1f);
      iVar18 = (int)lVar32;
      if ((int)sVar15 - 0x1fU == uVar21) break;
      lVar43 = lVar43 + -1;
    } while (lVar43 != 0);
  }
  uVar35 = uVar35 >> (uVar31 & 0x3f);
  lVar34 = lVar34 + uVar12;
  uVar12 = (ulonglong)uVar35 | 0x80000000;
  *(short *)((int)param_1 + 0x42) = (short)lVar34;
  param_1[0x11] = (int)uVar12;
  if ((uVar35 & 1) != 0) {
    do {
      uVar12 = uVar12 >> 1;
      lVar34 = lVar34 + 1;
    } while ((uVar12 & 1) != 0);
    param_1[0x11] = (int)uVar12;
    *(short *)((int)param_1 + 0x42) = (short)lVar34;
  }
  uVar31 = 1;
  param_1[0x21] = iVar18 + param_1[0x21];
  *(short *)(param_1 + 0x2c) = (short)iVar18 + *(short *)(param_1 + 0x2c);
  *(ushort *)(param_1 + 0x26) = *(ushort *)(param_1 + 0x26) | 2;
LAB_00266d48:
  *(short *)(param_1 + 0x30) = (short)iVar20;
  local_11c = 0;
  _opd_FUN_0026c9e0(param_1 + 0x16);
  if (*(short *)(param_1[0xf] + 2) != 0xc) {
    local_11c = _opd_FUN_0026d350(*(short *)(param_1[0xf] + 2),0);
    _opd_FUN_00f9ac38(local_11c,param_1[0xf],*(undefined2 *)(param_1[0xf] + 2));
    _opd_FUN_00269808(param_1[0xf]);
  }
  uVar38 = uVar17 - 0x29e0;
  uVar12 = uVar38 & 0xffffffff;
  _opd_FUN_00269230(uVar12,local_11c);
  puVar19 = (uint *)_opd_FUN_00269240(uVar12);
  *(uint *)(param_1[0xf] + 4) = (uint)local_122;
  _opd_FUN_0026a0e8(uVar39,uVar9,iVar20);
  piVar14 = param_1 + 0x31;
  piVar37 = (int *)((int)param_1 + 0xc2);
  iVar20 = _opd_FUN_00269448(uVar39 & 0xffffffff);
  bVar1 = puVar19 == (uint *)0x0;
  while (iVar20 != 0) {
    uVar21 = _opd_FUN_002692c0(iVar20);
    uVar35 = uVar21 & 0xfff;
    iVar18 = _opd_FUN_002614f8(uVar35,param_1);
    if (iVar18 < 0) {
      if ((((uVar21 & 0x1000) != 0) && (0x3f < uVar35)) &&
         (_opd_FUN_00261ab0(uVar17 - 0x140 & 0xffffffff,uVar35 | 0x8000,param_1), -1 < local_140)) {
        _opd_FUN_0026b2b0(uVar33 & 0xffffffff,0,3);
        iVar18 = local_140;
        goto joined_r0x00266e84;
      }
    }
    else {
joined_r0x00266e84:
      while ((!bVar1 && ((*puVar19 & 0xfff) < uVar35))) {
        *(undefined1 *)((int)puVar19 + 0xb) = 0;
        iVar22 = _opd_FUN_002696d8(param_1[0xf],puVar19,0);
        if (iVar22 < 0) {
          (*(code *)**(undefined4 **)(*param_1 + 8))(param_1);
        }
        if ((*(ushort *)(param_1 + 5) & 0x40) != 0) {
          _opd_FUN_00261438(*puVar19);
        }
        _opd_FUN_00269280(uVar38 & 0xffffffff);
        puVar19 = (uint *)_opd_FUN_00269240(uVar38 & 0xffffffff);
        bVar1 = puVar19 == (uint *)0x0;
      }
      iVar22 = _opd_FUN_00261438(iVar18);
      bVar40 = *(byte *)(iVar22 + 10) | 0x20;
      *(byte *)(iVar22 + 10) = bVar40;
      if ((uVar21 & 0x4000) == 0) {
        if ((uVar21 & 0x1000) == 0) {
          if ((uVar21 & 0x8000) != 0) {
            *(ushort *)(param_1 + 0x26) = *(ushort *)(param_1 + 0x26) | 4;
            *(short *)((int)param_1 + 0xb2) = *(short *)((int)param_1 + 0xb2) + 1;
            bVar40 = *(byte *)(iVar22 + 10);
          }
          if (((bVar40 & 2) == 0) || (uVar31 != 0)) {
LAB_00267738:
            iVar20 = _opd_FUN_00269b00(param_1[0xf],iVar20,iVar18);
            if (iVar20 < 0) {
              (*(code *)**(undefined4 **)(*param_1 + 8))(param_1);
            }
          }
        }
        else {
          cVar29 = _opd_FUN_00269348(iVar20);
          if (((uVar21 & 0x8000) != 0) && (iVar24 = _opd_FUN_00261600(), iVar24 < 200)) {
            _opd_FUN_0026c9e0(param_1 + 0x1a);
            *(char *)((int)param_1 + 0x55) = cVar29;
            param_1[0x20] = iVar18;
          }
          iVar24 = _opd_FUN_002696d8(param_1[0xd],local_98 & 0xffffffff,0);
          if (iVar24 < 0) {
            (*(code *)**(undefined4 **)(*param_1 + 8))(param_1);
            break;
          }
          if (((*(byte *)(iVar22 + 10) & 2) == 0) ||
             ((char)(*(char *)(iVar22 + 0xe) - cVar29) < '\x01')) {
            if (!bVar1) {
              uVar21 = *puVar19;
              cVar29 = _opd_FUN_00269348(iVar20);
              if (uVar35 == (uVar21 & 0xfff)) {
                cVar30 = *(char *)((int)puVar19 + 10);
                bVar11 = false;
                if ((char)(cVar30 - cVar29) < '\x01') {
                  do {
                    if (cVar30 == cVar29) {
                      *(undefined1 *)((int)puVar19 + 0xb) = 0;
                      bVar11 = true;
                      iVar22 = _opd_FUN_002696d8(param_1[0xf],puVar19,0);
                    }
                    else {
                      *(undefined1 *)((int)puVar19 + 0xb) = 0;
                      iVar22 = _opd_FUN_002696d8(param_1[0xf],puVar19,0);
                    }
                    if (iVar22 < 0) {
                      (*(code *)**(undefined4 **)(*param_1 + 8))(param_1);
                    }
                    _opd_FUN_00269280(uVar12);
                    puVar19 = (uint *)_opd_FUN_00269240(uVar12);
                    bVar1 = puVar19 == (uint *)0x0;
                  } while (((!bVar1) && ((*puVar19 & 0xfff) == (uVar21 & 0xfff))) &&
                          (cVar30 = *(char *)((int)puVar19 + 10), (char)(cVar30 - cVar29) < '\x01'))
                  ;
                  if (bVar11) goto LAB_002674d8;
                }
              }
            }
            goto LAB_00267738;
          }
        }
      }
      else {
        cVar29 = *(char *)(iVar22 + 0xc);
        bVar40 = *(byte *)(iVar22 + 0xd);
        if ((param_1[0x1f] == iVar18) &&
           (cVar30 = _opd_FUN_00269348(iVar20), *(char *)(param_1 + 0x15) == cVar30)) {
          if (((uVar21 & 0x8000) != 0) &&
             (((sVar15 = _opd_FUN_002692e0(iVar20), sVar15 == 1 &&
               (pbVar26 = (byte *)_opd_FUN_00269320(iVar20), *pbVar26 != 0xff)) &&
              (iVar18 = _opd_FUN_00261600(), iVar18 < 200)))) {
            sVar15 = _opd_FUN_0026c9f8(param_1 + 0x18);
            if (piVar37 < piVar14) {
              *(undefined2 *)(param_1 + 0x32) = *(undefined2 *)((int)param_1 + 0xc6);
              *(undefined2 *)((int)param_1 + 0xc6) = *(undefined2 *)(param_1 + 0x31);
              *(undefined2 *)piVar14 = *(undefined2 *)piVar37;
            }
            else {
              uVar2 = *(undefined2 *)(param_1 + 0x31);
              uVar3 = *(undefined2 *)((int)param_1 + 0xc6);
              *(undefined2 *)piVar14 = *(undefined2 *)piVar37;
              *(undefined2 *)((int)param_1 + 0xc6) = uVar2;
              *(undefined2 *)(param_1 + 0x32) = uVar3;
            }
            *(short *)((int)param_1 + 0xc2) = sVar15;
            if ((short)(ushort)*pbVar26 < sVar15) {
              sVar15 = sVar15 - (ushort)*pbVar26;
              *(short *)((int)param_1 + 0xc2) = sVar15;
              if (600 < sVar15) {
                if (sVar15 < 0x3e9) {
                  *(ushort *)(param_1 + 5) = *(ushort *)(param_1 + 5) | 0x20;
                }
                else {
                  iVar18 = (((param_1[0x24] * 0x2ee) / 1000) * 0x2ee) / 1000;
                  param_1[0x24] = iVar18;
                  if (iVar18 < 16000) {
                    param_1[0x24] = 16000;
                  }
                }
              }
            }
            else {
              *(undefined2 *)((int)param_1 + 0xc2) = 0;
            }
          }
          param_1[0x1f] = -1;
          param_1[0x27] = param_1[0x27] + 1;
        }
        _opd_FUN_00269230(uVar33 & 0xffffffff,param_1[0xe]);
        while (puVar23 = (uint *)_opd_FUN_00269240(uVar33 & 0xffffffff), puVar23 != (uint *)0x0) {
          if ((*puVar23 & 0xfff) == uVar35) {
            cVar30 = *(char *)((int)puVar23 + 10);
            cVar28 = _opd_FUN_00269348(iVar20);
            if (cVar30 == cVar28) {
              *(byte *)((int)puVar23 + 9) = *(byte *)((int)puVar23 + 9) | 0x80;
            }
            else if ((-1 < *(char *)((int)puVar23 + 9)) &&
                    (bVar16 = *(char *)((int)puVar23 + 10) - cVar29, bVar16 < bVar40)) {
              bVar40 = bVar16;
            }
          }
          _opd_FUN_00269280(uVar33 & 0xffffffff);
        }
        _opd_FUN_00269230(uVar33 & 0xffffffff,param_1[0xd]);
        while (puVar23 = (uint *)_opd_FUN_00269240(uVar33 & 0xffffffff), puVar23 != (uint *)0x0) {
          if (((*puVar23 & 0xfff) == uVar35) && ((*(byte *)((int)puVar23 + 9) & 2) == 0)) {
            cVar30 = *(char *)((int)puVar23 + 10);
            cVar28 = _opd_FUN_00269348(iVar20);
            if (cVar30 == cVar28) {
              *(byte *)((int)puVar23 + 9) = *(byte *)((int)puVar23 + 9) | 0xc0;
            }
            else if ((-1 < *(char *)((int)puVar23 + 9)) &&
                    (bVar16 = *(char *)((int)puVar23 + 10) - cVar29, bVar16 < bVar40)) {
              bVar40 = bVar16;
            }
          }
          _opd_FUN_00269280(uVar33 & 0xffffffff);
        }
        *(byte *)(iVar22 + 0xc) = bVar40 + cVar29;
        *(byte *)(iVar22 + 0xd) = (cVar29 + *(char *)(iVar22 + 0xd)) - (bVar40 + cVar29);
      }
    }
LAB_002674d8:
    _opd_FUN_00269480(uVar39 & 0xffffffff);
    iVar20 = _opd_FUN_00269448(uVar39 & 0xffffffff);
  }
  _opd_FUN_00269668(uVar39);
  if (!bVar1) {
    uVar38 = uVar38 & 0xffffffff;
    do {
      while( true ) {
        *(undefined1 *)((int)puVar19 + 0xb) = 0;
        iVar20 = _opd_FUN_002696d8(param_1[0xf],puVar19,0);
        if (iVar20 < 0) {
          (*(code *)**(undefined4 **)(*param_1 + 8))(param_1);
        }
        if ((*(ushort *)(param_1 + 5) & 0x40) == 0) break;
        _opd_FUN_00261438(*puVar19);
        _opd_FUN_00269280(uVar38);
        puVar19 = (uint *)_opd_FUN_00269240(uVar38);
        if (puVar19 == (uint *)0x0) goto LAB_002675a4;
      }
      _opd_FUN_00269280(uVar38);
      puVar19 = (uint *)_opd_FUN_00269240(uVar38);
    } while (puVar19 != (uint *)0x0);
  }
LAB_002675a4:
  if (local_11c != 0) {
    _opd_FUN_0026d250(local_11c);
  }
  *(ushort *)(param_1 + 5) =
       *(ushort *)(param_1 + 5) & 0xff80 |
       (ushort)((((ulonglong)*(ushort *)(param_1 + 5) & 0x3f) << 0x19) >> 0x10) >> 9;
  return 1;
}

