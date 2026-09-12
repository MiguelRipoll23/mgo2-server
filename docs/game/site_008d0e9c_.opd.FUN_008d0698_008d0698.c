// hook SITE 0x008d0e9c

void _opd_FUN_008d0698(int param_1)

{
  bool bVar1;
  undefined2 uVar2;
  int iVar3;
  undefined *puVar4;
  int iVar5;
  undefined4 *puVar6;
  undefined *puVar7;
  byte bVar8;
  char cVar12;
  int iVar10;
  int iVar11;
  longlong lVar9;
  uint uVar13;
  ulonglong uVar14;
  uint uVar15;
  int *piVar16;
  undefined4 *puVar17;
  uint uVar18;
  int iVar19;
  int iVar20;
  undefined4 uVar21;
  undefined4 uVar22;
  undefined4 uVar23;
  undefined4 uVar24;
  undefined4 uVar25;
  undefined4 uVar26;
  undefined4 uVar27;
  undefined4 uVar28;
  undefined4 uVar29;
  undefined4 uVar30;
  undefined4 uVar31;
  undefined4 uVar32;
  undefined4 uVar33;
  undefined4 uVar34;
  undefined4 uVar35;
  undefined1 local_60 [24];
  
  puVar7 = PTR_PTR_0121b948;
  if (*(int *)(param_1 + 0xa70) == 0) {
    uVar13 = *(uint *)(param_1 + 100);
    if (uVar13 == 7) {
      iVar10 = _opd_FUN_00ae9558(*(undefined4 *)(param_1 + 0x60));
      if (iVar10 == 0) {
        *(int *)(param_1 + 0xa74) =
             *(int *)(param_1 + 0xa74) + *(int *)(*(int *)(puVar7 + -0x7f28) + 0xc);
        return;
      }
      uVar13 = *(uint *)(param_1 + 100);
    }
    if (uVar13 < 10) {
                    /* WARNING: Could not recover jumptable at 0x008d070c. Too many branches */
                    /* WARNING: Treating indirect jump as call */
      (*(code *)(*(int *)(uVar13 * 4 + *(int *)(puVar7 + -0x7f24)) + *(int *)(puVar7 + -0x7f24)))();
      return;
    }
    if (*(int *)(param_1 + 0x500) == 0x8f) {
      iVar10 = *(int *)(param_1 + 0x70);
      *(undefined4 *)(param_1 + 0x168) = 0x5854ce;
      iVar11 = _opd_FUN_000c5ab0(0xd5854ce);
      if (iVar11 != 0) {
        iVar11 = _opd_FUN_00117da0(iVar11,0x10);
        *(int *)(param_1 + 0x178) = iVar11;
        if (iVar11 != 0) {
          uVar2 = *(undefined2 *)(iVar10 + 10);
          *(float *)(iVar11 + 0x14c) = *(float *)(iVar11 + 0x14c) * *(float *)(puVar7 + -0x7ff8);
          _opd_FUN_00113030(iVar11,uVar2);
          _opd_FUN_001126a8(iVar10,iVar11);
          *(undefined4 *)(iVar11 + 0x54) = *(undefined4 *)(iVar10 + 0x54);
          if (((*(uint *)(param_1 + 0x238) & 2) == 0) && (*(int *)(iVar11 + 0xac) != 0)) {
            uVar14 = 0;
            do {
              piVar16 = *(int **)(iVar10 + 0xd0);
              iVar19 = *(int *)((int)((uVar14 & 0xffffffff) << 4) +
                                (int)((uVar14 & 0xffffffff) << 6) + *(int *)(iVar11 + 0xd0));
              if ((int)*(uint *)(iVar10 + 0xac) < 1) {
LAB_008d0fa4:
                bVar8 = 0;
              }
              else {
                iVar20 = 0;
                lVar9 = ((ulonglong)*(uint *)(iVar10 + 0xac) - 1 & 0xffffffff) + 1;
                if (*piVar16 == iVar19) goto LAB_008d0fa4;
                do {
                  piVar16 = piVar16 + 0x14;
                  iVar20 = iVar20 + 1;
                  lVar9 = lVar9 + -1;
                  if (lVar9 == 0) goto LAB_008d0fa4;
                } while (iVar19 != *piVar16);
                lVar9 = ((longlong)((longlong)iVar20 - 0xffU) >> 0x3f & (longlong)iVar20 - 0xffU) +
                        0xff;
                bVar8 = (byte)lVar9 & ~(byte)(lVar9 >> 0x3f);
              }
              iVar19 = (int)uVar14;
              uVar14 = uVar14 + 1;
              *(byte *)(param_1 + iVar19 + 0x17c) = bVar8;
            } while ((uVar14 & 0xffffffff) < (ulonglong)*(uint *)(iVar11 + 0xac));
          }
        }
      }
      iVar10 = *(int *)(param_1 + 0xa78);
      cVar12 = '\x01';
    }
    else {
      cVar12 = _opd_FUN_008ced08(param_1);
      iVar10 = *(int *)(param_1 + 0xa78);
    }
    if ((iVar10 == 1) && (*(int *)(param_1 + 0xa7c) == 1)) {
      iVar10 = 0;
      iVar11 = param_1 + 0x78;
      do {
        if (*(int *)(iVar11 + 0x18) == 0) {
LAB_008d1080:
          if (*(longlong *)(iVar11 + 0xa0) != 0) {
LAB_008d0ff4:
            _opd_FUN_000c1208(*(undefined8 *)(iVar11 + 0xa0),0x190005,0x3c4b3240);
            _opd_FUN_000c1208(*(undefined8 *)(iVar11 + 0xa0),0x190003,local_60);
          }
        }
        else {
          if (*(longlong *)(iVar11 + 0xa0) != 0) goto LAB_008d0ff4;
          lVar9 = _opd_FUN_00d8f548(*(int *)(iVar11 + 0x18));
          *(longlong *)(iVar11 + 0xa0) = lVar9;
          if (lVar9 != 0) {
            _opd_FUN_000beea8(param_1,lVar9);
            goto LAB_008d1080;
          }
        }
        bVar1 = iVar10 != 9;
        iVar11 = iVar11 + 0xe8;
        iVar10 = iVar10 + 1;
      } while (bVar1);
      iVar10 = *(int *)(param_1 + 0x90);
      if ((iVar10 != 0) &&
         (((iVar11 = *(int *)(param_1 + 0x80), iVar11 == 0x284b44 || (iVar11 == 0xba0fb8)) ||
          (iVar11 == 0xbb0fb9)))) {
        puVar17 = *(undefined4 **)(iVar10 + 200);
        if (*(uint *)(iVar10 + 0xa4) != 0) {
          lVar9 = ((ulonglong)*(uint *)(iVar10 + 0xa4) - 1 & 0xffffffff) + 1;
          puVar4 = (undefined *)*puVar17;
          while (puVar4 != &DAT_00b8129e) {
            lVar9 = lVar9 + -1;
            if (lVar9 == 0) goto LAB_008d1c78;
            puVar17 = puVar17 + 4;
            puVar4 = (undefined *)*puVar17;
          }
          if (*(char *)((int)puVar17 + 7) != '\0') goto LAB_008d075c;
        }
LAB_008d1c78:
        _opd_FUN_00113118(iVar10,&DAT_00b8129e);
        *(undefined1 *)(param_1 + 0xa8c) = 1;
      }
    }
LAB_008d075c:
    if (cVar12 == '\0') {
      return;
    }
    *(undefined4 *)(param_1 + 0xa70) = 1;
  }
  iVar10 = *(int *)(param_1 + 0xa78);
  if (iVar10 == 1) {
    uVar14 = (ulonglong)(*(int *)(param_1 + 0xa84) - *(int *)(*(int *)(puVar7 + -0x7f28) + 0xc));
    uVar14 = uVar14 & ~((longlong)uVar14 >> 0x3f);
    *(int *)(param_1 + 0xa84) = (int)uVar14;
    iVar11 = (int)((uVar14 & 0xffffffff) << 7) / 0x96;
    iVar10 = _opd_FUN_00087620();
    if (iVar10 == 0) {
LAB_008d07e4:
      iVar10 = _opd_FUN_00034418();
      if ((iVar10 == 0) || (**(int **)(puVar7 + -0x7f1c) == 0)) goto LAB_008d0804;
    }
    else {
LAB_008d09d8:
      iVar10 = _opd_FUN_00087a78();
      if ((((iVar10 == 0) || (iVar10 = _opd_FUN_00087a78(), iVar10 == 0)) ||
          ((iVar10 = _opd_FUN_00087a78(), (*(uint *)(iVar10 + 8) & 0x1ff) != 6 ||
           ((*(short *)(**(int **)(puVar7 + -0x7f20) + 0xae2) != 1 ||
            (*(short *)(**(int **)(puVar7 + -0x7f20) + 0xb52) < 1)))))) &&
         ((iVar10 = _opd_FUN_00087a78(), iVar10 == 0 ||
          (((iVar10 = _opd_FUN_00087a78(), iVar10 == 0 ||
            (iVar10 = _opd_FUN_00087a78(), (*(uint *)(iVar10 + 8) & 0x1ff) != 0x16)) ||
           (iVar10 = _opd_FUN_003087f0(), *(int *)(iVar10 + 0x58) == 1)))))) goto LAB_008d07e4;
    }
LAB_008d0a38:
    *(undefined4 *)(param_1 + 0xa80) = 0x80;
    uVar13 = *(uint *)(*(int *)(param_1 + 0x70) + 0x5c);
  }
  else {
    iVar11 = 0;
    if (iVar10 != 2) {
      if (iVar10 != 0) goto LAB_008d0a38;
      uVar14 = (longlong)(*(int *)(param_1 + 0xa84) + *(int *)(*(int *)(puVar7 + -0x7f28) + 0xc)) -
               0x96;
      uVar14 = (uVar14 & (longlong)uVar14 >> 0x3f) + 0x96;
      *(int *)(param_1 + 0xa84) = (int)uVar14;
      iVar11 = (int)((uVar14 & 0xffffffff) << 7) / 0x96;
      iVar10 = _opd_FUN_00087620();
      if (iVar10 != 0) goto LAB_008d09d8;
      goto LAB_008d07e4;
    }
LAB_008d0804:
    *(int *)(param_1 + 0xa80) = iVar11;
    uVar13 = *(uint *)(*(int *)(param_1 + 0x70) + 0x5c);
  }
  if ((uVar13 & 7) == 0) {
    iVar11 = 0;
    _opd_FUN_00308538(*(undefined4 *)(param_1 + 0x60),99);
    iVar10 = param_1 + 0x78;
    piVar16 = (int *)(param_1 + 0x154);
    do {
      iVar19 = piVar16[-0x31];
      if (iVar19 != 0) {
        if (*(uint *)(param_1 + 100) < 10) {
                    /* WARNING: Could not recover jumptable at 0x008d0ac4. Too many branches */
                    /* WARNING: Treating indirect jump as call */
          (*(code *)(*(int *)(*(uint *)(param_1 + 100) * 4 + *(int *)(puVar7 + -0x7f18)) +
                    *(int *)(puVar7 + -0x7f18)))();
          return;
        }
        if (iVar11 == 10) {
          *piVar16 = *(int *)(param_1 + 0x154);
        }
        *(uint *)(iVar19 + 0x5c) = *(uint *)(iVar19 + 0x5c) & 0xfffffff8;
        if (*piVar16 == 0) {
          _opd_FUN_00112098(iVar19,0xb38bd6,0xf);
          _opd_FUN_00112098(iVar19,0x7a07aa,0xf);
        }
        else {
          _opd_FUN_00111bf0(iVar19,0xb38bd6,0xf);
          _opd_FUN_00111bf0(iVar19,0x7a07aa,0xf);
        }
      }
      iVar11 = iVar11 + 1;
      piVar16 = piVar16 + 0x3a;
    } while (iVar11 < 0xb);
    iVar11 = *(int *)(param_1 + 0x70);
    iVar19 = *(int *)(iVar11 + 0xb4);
    if (*(int *)(param_1 + 0xa80) < iVar19) {
      iVar19 = *(int *)(param_1 + 0xa80);
    }
    iVar20 = 0;
    while( true ) {
      iVar3 = *(int *)(iVar10 + 0x18);
      if ((iVar3 != 0) && (*(int *)(iVar3 + 0xb4) = iVar19, (*(uint *)(iVar10 + 0xd8) & 2) == 0)) {
        if (*(int *)(iVar3 + 0xac) != 0) {
          uVar13 = 0;
          do {
            iVar5 = uVar13 + iVar10;
            uVar18 = uVar13 * 0x40 + *(int *)(iVar3 + 0xd4);
            uVar13 = uVar13 + 1;
            uVar15 = (uint)*(byte *)(iVar5 + 0x1c) * 0x40 + *(int *)(iVar11 + 0xd4);
            puVar17 = (undefined4 *)(uVar15 + 0x30 & 0xfffffff0);
            uVar21 = puVar17[1];
            uVar22 = puVar17[2];
            uVar23 = puVar17[3];
            puVar6 = (undefined4 *)(uVar15 & 0xfffffff0);
            uVar24 = *puVar6;
            uVar25 = puVar6[1];
            uVar26 = puVar6[2];
            uVar27 = puVar6[3];
            puVar6 = (undefined4 *)(uVar15 + 0x10 & 0xfffffff0);
            uVar32 = *puVar6;
            uVar33 = puVar6[1];
            uVar34 = puVar6[2];
            uVar35 = puVar6[3];
            puVar6 = (undefined4 *)(uVar15 + 0x20 & 0xfffffff0);
            uVar28 = *puVar6;
            uVar29 = puVar6[1];
            uVar30 = puVar6[2];
            uVar31 = puVar6[3];
            puVar6 = (undefined4 *)(uVar18 + 0x30 & 0xfffffff0);
            *puVar6 = *puVar17;
            puVar6[1] = uVar21;
            puVar6[2] = uVar22;
            puVar6[3] = uVar23;
            puVar17 = (undefined4 *)(uVar18 & 0xfffffff0);
            *puVar17 = uVar24;
            puVar17[1] = uVar25;
            puVar17[2] = uVar26;
            puVar17[3] = uVar27;
            puVar17 = (undefined4 *)(uVar18 + 0x10 & 0xfffffff0);
            *puVar17 = uVar32;
            puVar17[1] = uVar33;
            puVar17[2] = uVar34;
            puVar17[3] = uVar35;
            puVar17 = (undefined4 *)(uVar18 + 0x20 & 0xfffffff0);
            *puVar17 = uVar28;
            puVar17[1] = uVar29;
            puVar17[2] = uVar30;
            puVar17[3] = uVar31;
          } while (uVar13 < *(uint *)(iVar3 + 0xac));
        }
        uVar13 = *(uint *)(iVar3 + 0xd4);
        puVar17 = (undefined4 *)(uVar13 & 0xfffffff0);
        uVar21 = puVar17[1];
        uVar22 = puVar17[2];
        uVar23 = puVar17[3];
        puVar6 = (undefined4 *)(uVar13 + 0x10 & 0xfffffff0);
        uVar24 = *puVar6;
        uVar25 = puVar6[1];
        uVar26 = puVar6[2];
        uVar27 = puVar6[3];
        puVar6 = (undefined4 *)(uVar13 + 0x20 & 0xfffffff0);
        uVar32 = *puVar6;
        uVar33 = puVar6[1];
        uVar34 = puVar6[2];
        uVar35 = puVar6[3];
        puVar6 = (undefined4 *)(uVar13 + 0x30 & 0xfffffff0);
        uVar28 = *puVar6;
        uVar29 = puVar6[1];
        uVar30 = puVar6[2];
        uVar31 = puVar6[3];
        puVar6 = (undefined4 *)(iVar3 + 0x10U & 0xfffffff0);
        *puVar6 = *puVar17;
        puVar6[1] = uVar21;
        puVar6[2] = uVar22;
        puVar6[3] = uVar23;
        *(int *)(iVar3 + 0xb4) = iVar19;
        puVar17 = (undefined4 *)(iVar3 + 0x20U & 0xfffffff0);
        *puVar17 = uVar24;
        puVar17[1] = uVar25;
        puVar17[2] = uVar26;
        puVar17[3] = uVar27;
        puVar17 = (undefined4 *)(iVar3 + 0x30U & 0xfffffff0);
        *puVar17 = uVar32;
        puVar17[1] = uVar33;
        puVar17[2] = uVar34;
        puVar17[3] = uVar35;
        puVar17 = (undefined4 *)(iVar3 + 0x40U & 0xfffffff0);
        *puVar17 = uVar28;
        puVar17[1] = uVar29;
        puVar17[2] = uVar30;
        puVar17[3] = uVar31;
      }
      bVar1 = iVar20 == 10;
      iVar20 = iVar20 + 1;
      if (bVar1) break;
      iVar10 = iVar10 + 0xe8;
    }
  }
  else {
    iVar10 = *(int *)(param_1 + 0x90);
    if (iVar10 != 0) {
      *(uint *)(iVar10 + 0x5c) = *(uint *)(iVar10 + 0x5c) | 7;
    }
    iVar10 = *(int *)(param_1 + 0x178);
    if (iVar10 != 0) {
      *(uint *)(iVar10 + 0x5c) = *(uint *)(iVar10 + 0x5c) | 7;
    }
    iVar10 = *(int *)(param_1 + 0x260);
    if (iVar10 != 0) {
      *(uint *)(iVar10 + 0x5c) = *(uint *)(iVar10 + 0x5c) | 7;
    }
    iVar10 = *(int *)(param_1 + 0x348);
    if (iVar10 != 0) {
      *(uint *)(iVar10 + 0x5c) = *(uint *)(iVar10 + 0x5c) | 7;
    }
    iVar10 = *(int *)(param_1 + 0x430);
    if (iVar10 != 0) {
      *(uint *)(iVar10 + 0x5c) = *(uint *)(iVar10 + 0x5c) | 7;
    }
    iVar10 = *(int *)(param_1 + 0x518);
    if (iVar10 != 0) {
      *(uint *)(iVar10 + 0x5c) = *(uint *)(iVar10 + 0x5c) | 7;
    }
    iVar10 = *(int *)(param_1 + 0x600);
    if (iVar10 != 0) {
      *(uint *)(iVar10 + 0x5c) = *(uint *)(iVar10 + 0x5c) | 7;
    }
    iVar10 = *(int *)(param_1 + 0x6e8);
    if (iVar10 != 0) {
      *(uint *)(iVar10 + 0x5c) = *(uint *)(iVar10 + 0x5c) | 7;
    }
    iVar10 = *(int *)(param_1 + 2000);
    if (iVar10 != 0) {
      *(uint *)(iVar10 + 0x5c) = *(uint *)(iVar10 + 0x5c) | 7;
    }
    iVar10 = *(int *)(param_1 + 0x8b8);
    if (iVar10 != 0) {
      *(uint *)(iVar10 + 0x5c) = *(uint *)(iVar10 + 0x5c) | 7;
    }
    iVar10 = *(int *)(param_1 + 0x9a0);
    if (iVar10 != 0) {
      *(uint *)(iVar10 + 0x5c) = *(uint *)(iVar10 + 0x5c) | 7;
    }
  }
  return;
}

