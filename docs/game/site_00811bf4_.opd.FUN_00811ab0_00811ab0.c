// hook SITE 0x00811bf4

void _opd_FUN_00811ab0(int param_1)

{
  uint uVar1;
  longlong lVar2;
  float fVar3;
  float fVar4;
  undefined4 uVar5;
  code *pcVar6;
  undefined1 auVar7 [16];
  undefined1 auVar8 [16];
  undefined1 auVar9 [16];
  ulonglong *puVar10;
  undefined4 *puVar11;
  undefined *puVar12;
  ulonglong uVar13;
  ulonglong uVar14;
  int iVar15;
  int iVar16;
  int *piVar17;
  char cVar18;
  undefined1 uVar19;
  short sVar21;
  undefined8 uVar20;
  ushort uVar22;
  ulonglong uVar23;
  ulonglong unaff_r22;
  ulonglong unaff_r23;
  ulonglong unaff_r24;
  ulonglong unaff_r25;
  ulonglong uVar24;
  ulonglong unaff_r26;
  ulonglong unaff_r27;
  ulonglong unaff_r28;
  ulonglong unaff_r29;
  ulonglong unaff_r30;
  ulonglong unaff_r31;
  ulonglong in_LR;
  double dVar25;
  ulonglong in_f31;
  undefined1 auVar26 [16];
  undefined1 local_2e0 [16];
  
  uVar14 = ZEXT48(&stack0x00000000);
  lVar2 = uVar14 - 0x380;
  puVar10 = (ulonglong *)lVar2;
  *puVar10 = uVar14;
  puVar10[0x6e] = unaff_r31;
  puVar10[0x6f] = in_f31;
  puVar10[0x66] = unaff_r23;
  puVar10[0x67] = unaff_r24;
  puVar10[0x69] = unaff_r26;
  puVar10[0x6d] = unaff_r30;
  puVar12 = PTR_PTR_0121b844;
  puVar10[0x65] = unaff_r22;
  puVar10[0x72] = in_LR;
  puVar10[0x68] = unaff_r25;
  puVar10[0x6a] = unaff_r27;
  puVar10[0x6b] = unaff_r28;
  puVar10[0x6c] = unaff_r29;
  iVar16 = *(int *)(param_1 + 0x18);
  iVar15 = _opd_FUN_002f8250(param_1,0x927ee8);
  if (*(int *)(iVar15 + 0x30) == 0) {
    *(undefined1 *)((int)puVar10 + 0x7c) = 0xb;
    uVar24 = uVar14 - 0x1b4 & 0xffffffff;
    *(undefined4 *)((int)puVar10 + 0x84) = *(undefined4 *)(iVar15 + 100);
    *(short *)((int)puVar10 + 0x7e) = (short)*(undefined4 *)(iVar15 + 0x70);
    *(undefined4 *)(puVar10 + 0x11) = *(undefined4 *)(iVar15 + 0xd4);
    *(undefined4 *)((int)puVar10 + 0x8c) = *(undefined4 *)(iVar15 + 0xd8);
    _opd_FUN_0026cad8(uVar24,uVar14 - 0x1a8,0x100);
    _opd_FUN_0026cc10(uVar24,uVar14 - 0x304,1);
    _opd_FUN_0026cc10(uVar24,uVar14 - 0x2fc,4);
    _opd_FUN_0026cc10(uVar24,uVar14 - 0x302,2);
    _opd_FUN_0026cc10(uVar24,uVar14 - 0x2f8,4);
    _opd_FUN_0026cc10(uVar24,uVar14 - 0x2f4,4);
    _opd_FUN_002784b0(param_1 + 0x36c,uVar24);
  }
  iVar15 = _opd_FUN_002f8250(param_1,&DAT_001f3700);
  iVar15 = *(int *)(iVar15 + 0x74);
  *(int *)(puVar10 + 0x12) = iVar15;
  if (iVar15 != 0) {
    uVar24 = uVar14 - 0x1b4 & 0xffffffff;
    _opd_FUN_0026cad8(uVar24,uVar14 - 0x1a8,0x100);
    *(undefined1 *)((int)puVar10 + 0x84) = 0xc;
    _opd_FUN_0026cc10(uVar24,uVar14 - 0x2fc,1);
    _opd_FUN_0026cc10(uVar24,uVar14 - 0x2f0,4);
    _opd_FUN_002784b0(param_1 + 0x36c,uVar24);
  }
  uVar24 = uVar14 - 0x2c0;
  _opd_FUN_0026cad8(uVar24,uVar14 - 0x2b4,0x100);
  uVar5 = *(undefined4 *)(param_1 + 0x800);
  *(undefined1 *)(puVar10 + 0xe) = 2;
  uVar22 = 8;
  fVar3 = *(float *)(*(int *)(param_1 + 0x34) + 0x180);
  fVar4 = *(float *)(*(int *)(param_1 + 0x34) + 0x184);
  if (fVar4 <= fVar3) {
    if (fVar4 < fVar3) {
      uVar22 = 0x10;
    }
    else {
      uVar22 = 0;
    }
  }
  uVar13 = (ulonglong)*(uint *)(*(int *)(param_1 + 0x18) + 0x1328) ^ 2;
  *(uint *)(param_1 + 0x824) = (uint)*(ushort *)(param_1 + 0x92);
  uVar23 = (ulonglong)((int)uVar13 >> 0x1f);
  *(ushort *)((int)puVar10 + 0x74) =
       *(ushort *)(param_1 + 0x92) << 5 | (ushort)uVar5 & 0xff |
       (ushort)(((uVar23 ^ uVar13) - uVar23) + -1 >> 0x10) & 0x8000 | uVar22;
  _opd_FUN_0026cc10(uVar24 & 0xffffffff,uVar14 - 0x310,1);
  _opd_FUN_0026cc10(uVar24 & 0xffffffff,uVar14 - 0x30c,2);
  puVar11 = (undefined4 *)(iVar16 + 0x50U & 0xfffffff0);
  local_2e0._4_4_ = puVar11[1];
  local_2e0._0_4_ = *puVar11;
  local_2e0._8_4_ = puVar11[2];
  local_2e0._12_4_ = puVar11[3];
  iVar15 = *(int *)(param_1 + 0x90);
  if (iVar15 == 0x145) {
    sVar21 = *(short *)(iVar16 + 0x92);
    dVar25 = (double)*(float *)(puVar12 + -0x7f08);
LAB_00811e10:
    _opd_FUN_00165b30(dVar25,uVar14 - 0x2d0,sVar21);
    vectorAddFloatingPoint(local_2e0,*(undefined1 (*) [16])(puVar10 + 0x16));
  }
  else {
    if (0x145 < iVar15) {
      if (iVar15 != 0x164) {
        if (iVar15 < 0x165) {
          if (iVar15 == 0x14c) {
            dVar25 = (double)*(float *)(puVar12 + -0x7f30);
            sVar21 = *(short *)(iVar16 + 0x92) + 0x7fff;
          }
          else if (iVar15 < 0x14d) {
            if (iVar15 == 0x147) {
              sVar21 = *(short *)(iVar16 + 0x92);
              dVar25 = (double)*(float *)(puVar12 + -0x7f00);
            }
            else {
              if (iVar15 != 0x14b) goto LAB_00811e2c;
              sVar21 = *(short *)(iVar16 + 0x92);
              dVar25 = (double)*(float *)(puVar12 + -0x7f30);
            }
          }
          else {
            if (iVar15 != 0x159) {
              if (iVar15 == 0x161) goto LAB_008121c8;
              goto LAB_00811e2c;
            }
            dVar25 = (double)*(float *)(puVar12 + -0x7f10);
            sVar21 = *(short *)(iVar16 + 0x92) + 0x7fff;
          }
          goto LAB_00811e10;
        }
        if (iVar15 != 0x2c8) {
          if (iVar15 < 0x2c9) {
            if ((iVar15 == 0x167) || (iVar15 == 0x170)) {
              sVar21 = *(short *)(iVar16 + 0x92);
              dVar25 = (double)*(float *)(puVar12 + -0x7ef8);
              goto LAB_00811e10;
            }
          }
          else if (iVar15 == 0x2cc) {
LAB_00811c90:
            if (*(uint *)(iVar16 + 0xff4) < 5) {
                    /* WARNING: Could not recover jumptable at 0x00811cb8. Too many branches */
                    /* WARNING: Treating indirect jump as call */
              (*(code *)(*(int *)(*(uint *)(iVar16 + 0xff4) * 4 + *(int *)(puVar12 + -0x7f3c)) +
                        *(int *)(puVar12 + -0x7f3c)))();
              return;
            }
            if (*(int *)(iVar16 + 0xff4) != 0) {
              dVar25 = (double)*(float *)(puVar12 + -0x7f30);
              sVar21 = *(short *)(iVar16 + 0x92) + *(short *)(puVar10 + 0xf);
              goto LAB_00811e10;
            }
          }
          else if (iVar15 == 0x2dd) goto LAB_008121c8;
          goto LAB_00811e2c;
        }
      }
LAB_008121c8:
      if (*(uint *)(iVar16 + 0xff4) < 5) {
                    /* WARNING: Could not recover jumptable at 0x008122a8. Too many branches */
                    /* WARNING: Treating indirect jump as call */
        (*(code *)(*(int *)(*(uint *)(iVar16 + 0xff4) * 4 + *(int *)(puVar12 + -0x7f38)) +
                  *(int *)(puVar12 + -0x7f38)))();
        return;
      }
      if (*(int *)(iVar16 + 0xff4) == 0) goto LAB_00811e2c;
      dVar25 = (double)*(float *)(puVar12 + -0x7f28);
      sVar21 = *(short *)(iVar16 + 0x92) + *(short *)(puVar10 + 0xf);
      goto LAB_00811e10;
    }
    if (iVar15 == 0x10) {
LAB_00812320:
      if (*(uint *)(iVar16 + 0xff4) < 5) {
                    /* WARNING: Could not recover jumptable at 0x00812348. Too many branches */
                    /* WARNING: Treating indirect jump as call */
        (*(code *)(*(int *)(*(uint *)(iVar16 + 0xff4) * 4 + *(int *)(puVar12 + -0x7f34)) +
                  *(int *)(puVar12 + -0x7f34)))();
        return;
      }
      if (*(int *)(iVar16 + 0xff4) == 0) goto LAB_00811e2c;
      dVar25 = (double)*(float *)(puVar12 + -0x7f10);
      sVar21 = *(short *)(iVar16 + 0x92) + *(short *)(puVar10 + 0xf);
      goto LAB_00811e10;
    }
    if (iVar15 < 0x11) {
      if (iVar15 == 0xd) {
LAB_00812618:
        sVar21 = *(short *)(iVar16 + 0x92);
        dVar25 = (double)*(float *)(puVar12 + -0x7f18);
      }
      else {
        if (0xd < iVar15) goto LAB_008121c8;
        if (iVar15 != 0xb) goto LAB_00811e2c;
        sVar21 = *(short *)(iVar16 + 0x92);
        dVar25 = (double)*(float *)(puVar12 + -0x7f10);
      }
      goto LAB_00811e10;
    }
    if (iVar15 == 0x25) {
      sVar21 = *(short *)(iVar16 + 0x92);
      dVar25 = (double)*(float *)(puVar12 + -0x7f20);
      goto LAB_00811e10;
    }
    if (iVar15 < 0x26) {
      if (iVar15 == 0x13) {
        sVar21 = *(short *)(iVar16 + 0x92);
        dVar25 = (double)*(float *)(puVar12 + -0x7f44);
        goto LAB_00811e10;
      }
      if (iVar15 == 0x18) goto LAB_00811c90;
    }
    else {
      if (iVar15 == 0x26) goto LAB_00812618;
      if (iVar15 == 0x27) goto LAB_00812320;
    }
  }
LAB_00811e2c:
  uVar13 = uVar24 & 0xffffffff;
  *(short *)(puVar10 + 0x10) = (short)*(undefined4 *)(puVar10 + 0x5c);
  *(short *)((int)puVar10 + 0x76) = (short)*(undefined4 *)(puVar10 + 0x5c);
  *(short *)((int)puVar10 + 0x7a) = (short)*(undefined4 *)(puVar10 + 0x5c);
  *(undefined2 *)(puVar10 + 0xf) = *(undefined2 *)(iVar16 + 0x92);
  _opd_FUN_0026cc10(uVar13,uVar14 - 0x300,2);
  _opd_FUN_0026cc10(uVar13,uVar14 - 0x30a,2);
  _opd_FUN_0026cc10(uVar13,uVar14 - 0x306,2);
  _opd_FUN_0026cc10(uVar13,uVar14 - 0x308,2);
  iVar16 = _opd_FUN_002f8250(param_1,0x1e05d5);
  if ((iVar16 == 0) || (*(int *)(iVar16 + 0x30) != 0)) {
    _opd_FUN_002f8250(param_1,0x7e8110);
    *(char *)((int)puVar10 + 0x71) = (char)*(undefined4 *)(puVar10 + 0x5c);
  }
  else {
    *(char *)((int)puVar10 + 0x71) = (char)*(undefined4 *)(puVar10 + 0x5c);
  }
  uVar13 = uVar24 & 0xffffffff;
  *(char *)((int)puVar10 + 0x72) = (char)*(undefined4 *)(puVar10 + 0x5c);
  _opd_FUN_0026cc10(uVar13,uVar14 - 0x30f,1);
  _opd_FUN_0026cc10(uVar13,uVar14 - 0x30e,1);
  iVar16 = *(int *)(param_1 + 0x90);
  if (iVar16 < 0x112) {
    if (0xfb < iVar16) {
LAB_00812110:
      piVar17 = (int *)_opd_FUN_002f8250(param_1,0x80dd2a);
      pcVar6 = (code *)**(undefined4 **)(*piVar17 + 0xc0);
      puVar10[5] = ZEXT48(&DAT_01222a00);
      uVar19 = (*pcVar6)(piVar17);
      *(undefined1 *)((int)puVar10 + 0x84) = uVar19;
      _opd_FUN_0026cc10(uVar24,uVar14 - 0x2fc,1);
      goto LAB_00812168;
    }
    if (iVar16 < 0x67) {
      if (iVar16 < 0x65) {
        if (iVar16 < 0x4e) {
          if (iVar16 < 0x4b) {
            if (iVar16 < 0x3f) goto LAB_00812168;
            if (iVar16 < 0x42) {
              iVar16 = _opd_FUN_002f8250(param_1,0x7e8110);
              uVar19 = (undefined1)*(undefined4 *)(iVar16 + 0x240);
              goto LAB_008126a8;
            }
            if (2 < iVar16 - 0x46U) goto LAB_00812168;
            iVar16 = _opd_FUN_002ec650(*(undefined4 *)(param_1 + 0x74));
            *(undefined1 *)((int)puVar10 + 0x84) = 0;
            if (iVar16 != 0) {
              *(undefined1 *)((int)puVar10 + 0x84) = 1;
            }
          }
          else {
            iVar16 = _opd_FUN_002f8250(param_1,0xcf5135);
            if (*(longlong *)(iVar16 + 0x290) == 0) {
              *(undefined1 *)((int)puVar10 + 0x84) = 0xff;
            }
            else {
              uVar19 = _opd_FUN_00818620();
              *(undefined1 *)((int)puVar10 + 0x84) = uVar19;
            }
          }
          goto LAB_008124c8;
        }
        if (0x60 < iVar16) {
          uVar1 = iVar16 - 0x62;
          goto joined_r0x008127d8;
        }
        if (iVar16 < 0x5f) {
          if (1 < iVar16 - 0x50U) goto LAB_00812168;
          uVar20 = 0x57afc8;
          goto LAB_0081250c;
        }
      }
    }
    else if (iVar16 < 0x70) {
      if (iVar16 < 0x6e) {
        if (iVar16 < 0x68) goto LAB_00812168;
        if (0x69 < iVar16) {
          uVar1 = iVar16 - 0x6b;
          goto joined_r0x008127d8;
        }
      }
    }
    else {
      if (0x75 < iVar16) {
        if (iVar16 - 0x9aU < 0x10) {
          iVar16 = _opd_FUN_002f8250(param_1,0x975d91);
          cVar18 = _opd_FUN_0084c068(iVar16,*(undefined4 *)(iVar16 + 0x58));
          *(char *)((int)puVar10 + 0x7c) = cVar18;
          *(char *)((int)puVar10 + 0x84) = (char)*(undefined2 *)(iVar16 + 0x82);
          if (-1 < cVar18) {
            _opd_FUN_0026cc10(uVar24 & 0xffffffff,uVar14 - 0x304,1);
            _opd_FUN_0026cc10(uVar24 & 0xffffffff,uVar14 - 0x2fc,1);
          }
        }
        goto LAB_00812168;
      }
      if (iVar16 < 0x74) {
        uVar1 = iVar16 - 0x71;
joined_r0x008127d8:
        if (1 < uVar1) goto LAB_00812168;
      }
    }
    iVar16 = _opd_FUN_002f8250(param_1,0xd9728f);
    *(byte *)((int)puVar10 + 0x84) = (byte)((ulonglong)*(undefined8 *)(iVar16 + 0x260) >> 0x3e) & 1;
    _opd_FUN_0026cc10(uVar24,uVar14 - 0x2fc,1);
  }
  else {
    if (iVar16 == 0x1e1) {
LAB_00812068:
      _opd_FUN_002f8250(param_1,0xdc8d0a);
      uVar13 = (longlong)*(int *)(puVar10 + 0x5c) - 0x78U &
               (longlong)((longlong)*(int *)(puVar10 + 0x5c) - 0x78U) >> 0x3f;
      cVar18 = (char)uVar13 + 'x';
      *(byte *)((int)puVar10 + 0x84) =
           cVar18 + ((byte)((longlong)(uVar13 + 0xf0) >> 0x3f) & 0x88U - cVar18);
      _opd_FUN_0026cc10(uVar24,uVar14 - 0x2fc,1);
      goto LAB_00812168;
    }
    if (iVar16 < 0x1e2) {
      if (0x192 < iVar16) {
        if (iVar16 < 0x1c4) {
          if (0x1c1 < iVar16) {
            iVar16 = _opd_FUN_002f8250(param_1,0x7e0b7a);
            uVar19 = (undefined1)*(undefined4 *)(iVar16 + 0x4c);
LAB_008126a8:
            *(undefined1 *)((int)puVar10 + 0x84) = uVar19;
            _opd_FUN_0026cc10(uVar13,uVar14 - 0x2fc,1);
            goto LAB_00812168;
          }
          if (2 < iVar16 - 0x196U) goto LAB_00812168;
          uVar20 = 0x73deac;
          goto LAB_008124ac;
        }
        if (iVar16 != 0x1df) goto LAB_00812168;
        goto LAB_00812068;
      }
      if (0x18c < iVar16) goto LAB_008122d8;
      if (iVar16 < 0x143) {
        if (0x13b < iVar16) {
          iVar16 = _opd_FUN_002f8250(param_1,0x80dd2a);
          iVar16 = *(int *)(iVar16 + 0x88);
          *(undefined1 *)((int)puVar10 + 0x84) = 0xff;
          if (iVar16 != 0) {
            uVar19 = _opd_FUN_00818620(*(undefined8 *)(iVar16 + 0x10));
            *(undefined1 *)((int)puVar10 + 0x84) = uVar19;
          }
          _opd_FUN_0026cc10(uVar13,uVar14 - 0x2fc,1);
          goto LAB_00812168;
        }
        if (iVar16 < 0x113) goto LAB_00812168;
        goto LAB_00812110;
      }
      if (6 < iVar16 - 0x154U) goto LAB_00812168;
      iVar16 = _opd_FUN_002f8250(param_1,0x6cf4bc);
      cVar18 = (char)*(undefined4 *)(iVar16 + 0x7c);
      *(char *)((int)puVar10 + 0x84) = cVar18;
    }
    else if (iVar16 < 0x1f2) {
      if (iVar16 < 0x1ee) {
        if (iVar16 != 0x1e7) {
          if (iVar16 < 0x1e8) {
            if ((iVar16 != 0x1e3) && (iVar16 != 0x1e5)) goto LAB_00812168;
          }
          else if (iVar16 != 0x1e9) {
            if ((iVar16 < 0x1e9) || (1 < iVar16 - 0x1ebU)) goto LAB_00812168;
            goto LAB_008124a0;
          }
        }
        goto LAB_00812068;
      }
LAB_008124a0:
      uVar20 = 0x7b5f88;
LAB_008124ac:
      iVar16 = _opd_FUN_002f8250(param_1,uVar20);
      cVar18 = (char)*(undefined4 *)(iVar16 + 0x4c);
      *(char *)((int)puVar10 + 0x84) = cVar18;
    }
    else {
      if (0x215 < iVar16) {
        if (iVar16 == 0x2cc) {
          iVar16 = _opd_FUN_002f8250(param_1,0x7e8110);
          *(char *)((int)puVar10 + 0x7c) = (char)*(undefined4 *)(iVar16 + 0x24c);
          _opd_FUN_0026cc10(uVar13,uVar14 - 0x304,1);
          goto LAB_00812168;
        }
        if (iVar16 != 0x2cd) goto LAB_00812168;
LAB_008122d8:
        iVar16 = _opd_FUN_002f8250(param_1,0x73deac);
        *(undefined2 *)((int)puVar10 + 0x7c) = *(undefined2 *)(iVar16 + 0x58);
        _opd_FUN_0026cc10(uVar24,uVar14 - 0x304,2);
        goto LAB_00812168;
      }
      if (0x212 < iVar16) {
        iVar16 = _opd_FUN_002f8250(param_1,&DAT_00de95a4);
        if ((iVar16 != 0) && (*(longlong *)(iVar16 + 0xd0) < 0)) {
          auVar26 = *(undefined1 (*) [16])(iVar16 + 0xc0U & 0xfffffff0);
          auVar7 = auVar26 >> 0x60;
          auVar8 = auVar26 >> 0x40;
          auVar9 = auVar26 >> 0x20;
          *(undefined1 (*) [16])(param_1 + 0x840U & 0xfffffff0) = auVar26;
          uVar5 = storeVectorElementWordIndexed
                            (auVar7 & (undefined1  [16])0xffffffff | auVar7 << 0x20 | auVar7 << 0x40
                             | auVar7 << 0x60,lVar2,0x2f0);
          *(undefined4 *)(puVar10 + 0x5e) = uVar5;
          uVar5 = storeVectorElementWordIndexed
                            (auVar8 & (undefined1  [16])0xffffffff | auVar8 << 0x20 |
                             (auVar8 & (undefined1  [16])0xffffffff) << 0x40 | auVar8 << 0x60,lVar2,
                             0x300);
          *(undefined4 *)(puVar10 + 0x60) = uVar5;
          uVar5 = storeVectorElementWordIndexed
                            (auVar9 & (undefined1  [16])0xffffffff | auVar9 << 0x20 |
                             (auVar9 & (undefined1  [16])0xffffffff) << 0x40 | auVar9 << 0x60,lVar2,
                             0x310);
          *(undefined4 *)(puVar10 + 0x62) = uVar5;
          *(short *)((int)puVar10 + 0x7e) = (short)*(undefined4 *)(puVar10 + 0x5c);
          *(short *)((int)puVar10 + 0x84) = (short)*(undefined4 *)(puVar10 + 0x5c);
          *(short *)((int)puVar10 + 0x7c) = (short)*(undefined4 *)(puVar10 + 0x5c);
          _opd_FUN_0026cc10(uVar13,uVar14 - 0x302,2);
          _opd_FUN_0026cc10(uVar13,uVar14 - 0x2fc,2);
          _opd_FUN_0026cc10(uVar13,uVar14 - 0x304,2);
        }
        goto LAB_00812168;
      }
      if (1 < iVar16 - 0x20eU) goto LAB_00812168;
      uVar20 = 0x236d7e;
LAB_0081250c:
      iVar16 = _opd_FUN_002f8250(param_1,uVar20);
      cVar18 = (char)*(undefined4 *)(iVar16 + 0x48);
      *(char *)((int)puVar10 + 0x84) = cVar18;
    }
    if (cVar18 < '\0') goto LAB_00812168;
LAB_008124c8:
    _opd_FUN_0026cc10(uVar24,uVar14 - 0x2fc,1);
  }
LAB_00812168:
  _opd_FUN_002784b0(param_1 + 0x36c,uVar24);
  return;
}

