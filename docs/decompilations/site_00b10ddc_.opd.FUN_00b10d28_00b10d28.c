// hook SITE 0x00b10ddc

void _opd_FUN_00b10d28(int param_1)

{
  bool bVar1;
  uint uVar2;
  undefined4 uVar3;
  undefined4 *puVar4;
  undefined *puVar5;
  uint *puVar6;
  uint *puVar7;
  uint *puVar8;
  undefined4 uVar9;
  undefined *puVar10;
  undefined4 uVar11;
  undefined4 uVar12;
  undefined4 uVar13;
  int iVar14;
  byte in_cr0;
  byte in_cr1;
  byte in_cr2;
  byte bVar15;
  byte in_cr3;
  byte bVar16;
  byte unaff_cr4;
  byte in_cr5;
  byte in_cr6;
  byte in_cr7;
  uint uStack00000008;
  float local_70;
  undefined4 uStack_6c;
  float local_68;
  undefined4 uStack_64;
  
  puVar5 = PTR_PTR_0121bd10;
  uStack00000008 =
       (uint)(in_cr0 & 0xf) << 0x1c | (uint)(in_cr1 & 0xf) << 0x18 | (uint)(in_cr2 & 0xf) << 0x14 |
       (uint)(in_cr3 & 0xf) << 0x10 | (uint)(unaff_cr4 & 0xf) << 0xc | (uint)(in_cr5 & 0xf) << 8 |
       (uint)(in_cr6 & 0xf) << 4 | (uint)(in_cr7 & 0xf);
  puVar6 = (uint *)_opd_FUN_00242bf0(*(undefined4 *)(param_1 + 0x60),0x57d678);
  bVar1 = puVar6 != (uint *)0x0;
  puVar7 = (uint *)_opd_FUN_00242bf0(*(undefined4 *)(param_1 + 0x60),0x57d679);
  puVar8 = (uint *)_opd_FUN_00242bf0(*(undefined4 *)(param_1 + 0x60),0x57d67a);
  uVar9 = _opd_FUN_00036350();
  puVar10 = (undefined *)_opd_FUN_000cdc90(uVar9);
  _opd_FUN_00b30830(*(undefined4 *)(param_1 + 0x60),&DAT_002b93f1);
  _opd_FUN_00b30830(*(undefined4 *)(param_1 + 0x60),0x1c090);
  uVar9 = _opd_FUN_00242bf0(*(undefined4 *)(param_1 + 0x60),0x1c090);
  *(undefined4 *)(param_1 + 0x84) = uVar9;
  if ((bVar1) && ((*puVar6 & 0x400000) != 0)) {
    *puVar6 = *puVar6 & 0xffbfffff | 0x40000000;
  }
  bVar16 = (puVar7 == (uint *)0x0) << 1;
  if ((puVar7 != (uint *)0x0) && ((*puVar7 & 0x400000) != 0)) {
    *puVar7 = *puVar7 & 0xffbfffff | 0x40000000;
  }
  bVar15 = (puVar8 == (uint *)0x0) << 1;
  if ((puVar8 == (uint *)0x0) || ((*puVar8 & 0x400000) == 0)) {
    *(undefined4 *)(param_1 + 0xfe0) = 0x80;
    *(undefined4 *)(param_1 + 0xfd8) = 0x80;
    *(undefined4 *)(param_1 + 0xfdc) = 0x80;
    if (puVar10 != &UNK_00f8c727) goto LAB_00b10e8c;
LAB_00b11070:
    if (bVar1) {
      if ((*puVar6 & 0x400000) == 0) {
        *puVar6 = *puVar6 | 0x40400000;
      }
    }
    uVar9 = *(undefined4 *)(puVar5 + -0x7c30);
    uVar11 = _opd_FUN_000cdc90(uVar9);
    uVar12 = _opd_FUN_000cdc90(*(undefined4 *)(puVar5 + -0x7bc0));
    _opd_FUN_00b30be8(*(undefined4 *)(param_1 + 0x60),0x1c090,uVar11,uVar12);
    uVar11 = *(undefined4 *)(param_1 + 0x60);
    uVar12 = _opd_FUN_00242bf0(uVar11,0x1c090);
    uVar11 = _opd_FUN_00255678(uVar11,uVar12);
    _opd_FUN_00255960(puVar6,uVar11);
    if ((!(bool)(bVar16 >> 1 & 1)) && ((*puVar7 & 0x400000) == 0)) {
      *puVar7 = *puVar7 | 0x40400000;
    }
    uVar11 = _opd_FUN_000cdc90(uVar9);
    uVar12 = _opd_FUN_000cdc90(*(undefined4 *)(puVar5 + -0x7bbc));
    _opd_FUN_00b30be8(*(undefined4 *)(param_1 + 0x60),&DAT_00381232,uVar11,uVar12);
    uVar11 = *(undefined4 *)(param_1 + 0x60);
    uVar12 = _opd_FUN_00242bf0(uVar11,&DAT_00381232);
    uVar11 = _opd_FUN_00255678(uVar11,uVar12);
    _opd_FUN_00255960(puVar7,uVar11);
    uVar2 = *(uint *)(param_1 + 0xfd8);
    *(uint *)(param_1 + 0xfdc) = ((int)uVar2 >> 1) + (uint)((int)uVar2 < 0 && (uVar2 & 1) != 0);
    if ((!(bool)(bVar15 >> 1 & 1)) && ((*puVar8 & 0x400000) == 0)) {
      *puVar8 = *puVar8 | 0x40400000;
    }
    uVar11 = _opd_FUN_000cdc90(uVar9);
    uVar9 = *(undefined4 *)(puVar5 + -0x7bb8);
LAB_00b111b4:
    uVar9 = _opd_FUN_000cdc90(uVar9);
LAB_00b111c8:
    _opd_FUN_00b30be8(*(undefined4 *)(param_1 + 0x60),&DAT_00381233,uVar11,uVar9);
    uVar9 = *(undefined4 *)(param_1 + 0x60);
    uVar11 = _opd_FUN_00242bf0(uVar9,&DAT_00381233);
    uVar9 = _opd_FUN_00255678(uVar9,uVar11);
    _opd_FUN_00255960(puVar8,uVar9);
    uVar2 = *(uint *)(param_1 + 0xfd8);
    *(uint *)(param_1 + 0xfe0) = ((int)uVar2 >> 1) + (uint)((int)uVar2 < 0 && (uVar2 & 1) != 0);
    goto LAB_00b11220;
  }
  *puVar8 = *puVar8 & 0xffbfffff | 0x40000000;
  *(undefined4 *)(param_1 + 0xfe0) = 0x80;
  *(undefined4 *)(param_1 + 0xfd8) = 0x80;
  *(undefined4 *)(param_1 + 0xfdc) = 0x80;
  if (puVar10 == &UNK_00f8c727) goto LAB_00b11070;
LAB_00b10e8c:
  if ((int)puVar10 < 0xf8c728) {
    if (puVar10 == &UNK_00f8c687) {
      if (bVar1) {
        if ((*puVar6 & 0x400000) == 0) {
          *puVar6 = *puVar6 | 0x40400000;
        }
      }
      uVar11 = _opd_FUN_000cdc90(*(undefined4 *)(puVar5 + -0x7c30));
      uVar9 = *(undefined4 *)(puVar5 + -0x7c04);
    }
    else {
      if (0xf8c687 < (int)puVar10) {
        if (puVar10 == &UNK_00f8c6c7) {
          if (bVar1) {
            if ((*puVar6 & 0x400000) == 0) {
              *puVar6 = *puVar6 | 0x40400000;
            }
          }
          uVar9 = *(undefined4 *)(puVar5 + -0x7c30);
          uVar11 = _opd_FUN_000cdc90(uVar9);
          uVar12 = _opd_FUN_000cdc90(*(undefined4 *)(puVar5 + -0x7c00));
          _opd_FUN_00b30be8(*(undefined4 *)(param_1 + 0x60),0x1c090,uVar11,uVar12);
          uVar11 = *(undefined4 *)(param_1 + 0x60);
          uVar12 = _opd_FUN_00242bf0(uVar11,0x1c090);
          uVar11 = _opd_FUN_00255678(uVar11,uVar12);
          _opd_FUN_00255960(puVar6,uVar11);
          if ((!(bool)(bVar16 >> 1 & 1)) && ((*puVar7 & 0x400000) == 0)) {
            *puVar7 = *puVar7 | 0x40400000;
          }
          uVar9 = _opd_FUN_000cdc90(uVar9);
          uVar11 = _opd_FUN_000cdc90(*(undefined4 *)(puVar5 + -0x7bfc));
          _opd_FUN_00b30be8(*(undefined4 *)(param_1 + 0x60),&DAT_00381232,uVar9,uVar11);
          uVar9 = *(undefined4 *)(param_1 + 0x60);
          uVar11 = _opd_FUN_00242bf0(uVar9,&DAT_00381232);
          uVar9 = _opd_FUN_00255678(uVar9,uVar11);
          _opd_FUN_00255960(puVar7,uVar9);
          uVar2 = *(uint *)(param_1 + 0xfd8);
          *(uint *)(param_1 + 0xfd8) =
               ((int)uVar2 >> 1) + (uint)((int)uVar2 < 0 && (uVar2 & 1) != 0);
          goto LAB_00b11220;
        }
        if ((int)puVar10 < 0xf8c6c8) {
          if (puVar10 == &UNK_00f8c6a7) goto LAB_00b117b8;
        }
        else {
          if (puVar10 == &UNK_00f8c6e7) goto LAB_00b11a1c;
          if (puVar10 == &UNK_00f8c707) goto LAB_00b11564;
        }
        goto LAB_00b11380;
      }
      if (puVar10 == (undefined *)0x494ec7) {
LAB_00b11a1c:
        iVar14 = _opd_FUN_00b19a70();
        if (iVar14 == 0) {
          uVar9 = _opd_FUN_000cdc90(*(undefined4 *)(puVar5 + -0x7c1c));
          uVar11 = _opd_FUN_000cdc90(*(undefined4 *)(puVar5 + -0x7c18));
          uVar12 = _opd_FUN_000cdc90(*(undefined4 *)(puVar5 + -0x7c14));
        }
        else {
          uVar9 = _opd_FUN_000cdc90(*(undefined4 *)(puVar5 + -0x7c28));
          uVar11 = _opd_FUN_000cdc90(*(undefined4 *)(puVar5 + -0x7c24));
          uVar12 = _opd_FUN_000cdc90(*(undefined4 *)(puVar5 + -0x7c20));
        }
        if ((bVar1) && ((*puVar6 & 0x400000) == 0)) {
          *puVar6 = *puVar6 | 0x40400000;
        }
        uVar3 = *(undefined4 *)(puVar5 + -0x7c30);
        uVar13 = _opd_FUN_000cdc90(uVar3);
        _opd_FUN_00b30be8(*(undefined4 *)(param_1 + 0x60),0x1c090,uVar13,uVar9);
        uVar9 = *(undefined4 *)(param_1 + 0x60);
        uVar13 = _opd_FUN_00242bf0(uVar9,0x1c090);
        uVar9 = _opd_FUN_00255678(uVar9,uVar13);
        _opd_FUN_00255960(puVar6,uVar9);
        if ((!(bool)(bVar16 >> 1 & 1)) && ((*puVar7 & 0x400000) == 0)) {
          *puVar7 = *puVar7 | 0x40400000;
        }
        uVar9 = _opd_FUN_000cdc90(uVar3);
        _opd_FUN_00b30be8(*(undefined4 *)(param_1 + 0x60),&DAT_00381232,uVar9,uVar11);
        uVar9 = *(undefined4 *)(param_1 + 0x60);
        uVar11 = _opd_FUN_00242bf0(uVar9,&DAT_00381232);
        uVar9 = _opd_FUN_00255678(uVar9,uVar11);
        _opd_FUN_00255960(puVar7,uVar9);
        uVar2 = *(uint *)(param_1 + 0xfd8);
        *(uint *)(param_1 + 0xfdc) = ((int)uVar2 >> 1) + (uint)((int)uVar2 < 0 && (uVar2 & 1) != 0);
        if ((!(bool)(bVar15 >> 1 & 1)) && ((*puVar8 & 0x400000) == 0)) {
          *puVar8 = *puVar8 | 0x40400000;
        }
        uVar9 = _opd_FUN_000cdc90(uVar3);
        _opd_FUN_00b30be8(*(undefined4 *)(param_1 + 0x60),&DAT_00381233,uVar9,uVar12);
        uVar9 = *(undefined4 *)(param_1 + 0x60);
        uVar11 = _opd_FUN_00242bf0(uVar9,&DAT_00381233);
        uVar9 = _opd_FUN_00255678(uVar9,uVar11);
        _opd_FUN_00255960(puVar8,uVar9);
        uVar2 = *(uint *)(param_1 + 0xfdc);
        *(uint *)(param_1 + 0xfe0) = ((int)uVar2 >> 1) + (uint)((int)uVar2 < 0 && (uVar2 & 1) != 0);
        goto LAB_00b11220;
      }
      if (puVar10 == (undefined *)0x494f07) {
LAB_00b11564:
        if (bVar1) {
          if ((*puVar6 & 0x400000) == 0) {
            *puVar6 = *puVar6 | 0x40400000;
          }
        }
        uVar9 = *(undefined4 *)(puVar5 + -0x7c30);
        uVar11 = _opd_FUN_000cdc90(uVar9);
        uVar12 = _opd_FUN_000cdc90(*(undefined4 *)(puVar5 + -0x7c10));
        _opd_FUN_00b30be8(*(undefined4 *)(param_1 + 0x60),0x1c090,uVar11,uVar12);
        uVar11 = *(undefined4 *)(param_1 + 0x60);
        uVar12 = _opd_FUN_00242bf0(uVar11,0x1c090);
        uVar11 = _opd_FUN_00255678(uVar11,uVar12);
        _opd_FUN_00255960(puVar6,uVar11);
        if ((!(bool)(bVar16 >> 1 & 1)) && ((*puVar7 & 0x400000) == 0)) {
          *puVar7 = *puVar7 | 0x40400000;
        }
        uVar11 = _opd_FUN_000cdc90(uVar9);
        uVar12 = _opd_FUN_000cdc90(*(undefined4 *)(puVar5 + -0x7c0c));
        _opd_FUN_00b30be8(*(undefined4 *)(param_1 + 0x60),&DAT_00381232,uVar11,uVar12);
        uVar11 = *(undefined4 *)(param_1 + 0x60);
        uVar12 = _opd_FUN_00242bf0(uVar11,&DAT_00381232);
        uVar11 = _opd_FUN_00255678(uVar11,uVar12);
        _opd_FUN_00255960(puVar7,uVar11);
        uVar2 = *(uint *)(param_1 + 0xfd8);
        *(uint *)(param_1 + 0xfdc) = ((int)uVar2 >> 1) + (uint)((int)uVar2 < 0 && (uVar2 & 1) != 0);
        if ((!(bool)(bVar15 >> 1 & 1)) && ((*puVar8 & 0x400000) == 0)) {
          *puVar8 = *puVar8 | 0x40400000;
        }
        uVar11 = _opd_FUN_000cdc90(uVar9);
        uVar9 = *(undefined4 *)(puVar5 + -0x7c08);
        goto LAB_00b111b4;
      }
      if (puVar10 != (undefined *)0x494b28) goto LAB_00b11380;
LAB_00b117b8:
      if (bVar1) {
        if ((*puVar6 & 0x400000) == 0) {
          *puVar6 = *puVar6 | 0x40400000;
        }
      }
      uVar11 = _opd_FUN_000cdc90(*(undefined4 *)(puVar5 + -0x7c30));
      uVar9 = *(undefined4 *)(puVar5 + -0x7c2c);
    }
  }
  else {
    if (puVar10 != &UNK_00f8caa7) {
      if ((int)puVar10 < 0xf8caa8) {
        if (puVar10 == &UNK_00f8c767) {
          if (bVar1) {
            if ((*puVar6 & 0x400000) == 0) {
              *puVar6 = *puVar6 | 0x40400000;
            }
          }
          uVar11 = _opd_FUN_000cdc90(*(undefined4 *)(puVar5 + -0x7c30));
          uVar9 = *(undefined4 *)(puVar5 + -0x7be0);
          goto LAB_00b11720;
        }
        if ((int)puVar10 < 0xf8c768) {
          if (puVar10 == &UNK_00f8c747) {
            if (bVar1) {
              if ((*puVar6 & 0x400000) == 0) {
                *puVar6 = *puVar6 | 0x40400000;
              }
            }
            uVar9 = *(undefined4 *)(puVar5 + -0x7c30);
            uVar11 = _opd_FUN_000cdc90(uVar9);
            uVar12 = _opd_FUN_000cdc90(*(undefined4 *)(puVar5 + -0x7bf8));
            _opd_FUN_00b30be8(*(undefined4 *)(param_1 + 0x60),0x1c090,uVar11,uVar12);
            uVar11 = *(undefined4 *)(param_1 + 0x60);
            uVar12 = _opd_FUN_00242bf0(uVar11,0x1c090);
            uVar11 = _opd_FUN_00255678(uVar11,uVar12);
            _opd_FUN_00255960(puVar6,uVar11);
            if ((!(bool)(bVar16 >> 1 & 1)) && ((*puVar7 & 0x400000) == 0)) {
              *puVar7 = *puVar7 | 0x40400000;
            }
            uVar11 = _opd_FUN_000cdc90(uVar9);
            uVar9 = *(undefined4 *)(puVar5 + -0x7bf4);
LAB_00b118b8:
            uVar9 = _opd_FUN_000cdc90(uVar9);
            _opd_FUN_00b30be8(*(undefined4 *)(param_1 + 0x60),&DAT_00381232,uVar11,uVar9);
            uVar9 = *(undefined4 *)(param_1 + 0x60);
            uVar11 = _opd_FUN_00242bf0(uVar9,&DAT_00381232);
            uVar9 = _opd_FUN_00255678(uVar9,uVar11);
            _opd_FUN_00255960(puVar7,uVar9);
            uVar2 = *(uint *)(param_1 + 0xfd8);
            *(uint *)(param_1 + 0xfdc) =
                 ((int)uVar2 >> 1) + (uint)((int)uVar2 < 0 && (uVar2 & 1) != 0);
            goto LAB_00b11220;
          }
        }
        else {
          if (puVar10 == &UNK_00f8c787) {
            if (bVar1) {
              if ((*puVar6 & 0x400000) == 0) {
                *puVar6 = *puVar6 | 0x40400000;
              }
            }
            uVar9 = *(undefined4 *)(puVar5 + -0x7c30);
            uVar11 = _opd_FUN_000cdc90(uVar9);
            uVar12 = _opd_FUN_000cdc90(*(undefined4 *)(puVar5 + -0x7bcc));
            _opd_FUN_00b30be8(*(undefined4 *)(param_1 + 0x60),0x1c090,uVar11,uVar12);
            uVar11 = *(undefined4 *)(param_1 + 0x60);
            uVar12 = _opd_FUN_00242bf0(uVar11,0x1c090);
            uVar11 = _opd_FUN_00255678(uVar11,uVar12);
            _opd_FUN_00255960(puVar6,uVar11);
            if ((!(bool)(bVar16 >> 1 & 1)) && ((*puVar7 & 0x400000) == 0)) {
              *puVar7 = *puVar7 | 0x40400000;
            }
            uVar11 = _opd_FUN_000cdc90(uVar9);
            uVar12 = _opd_FUN_000cdc90(*(undefined4 *)(puVar5 + -0x7bc8));
            _opd_FUN_00b30be8(*(undefined4 *)(param_1 + 0x60),&DAT_00381232,uVar11,uVar12);
            uVar11 = *(undefined4 *)(param_1 + 0x60);
            uVar12 = _opd_FUN_00242bf0(uVar11,&DAT_00381232);
            uVar11 = _opd_FUN_00255678(uVar11,uVar12);
            _opd_FUN_00255960(puVar7,uVar11);
            uVar2 = *(uint *)(param_1 + 0xfd8);
            *(uint *)(param_1 + 0xfdc) =
                 ((int)uVar2 >> 1) + (uint)((int)uVar2 < 0 && (uVar2 & 1) != 0);
            if ((!(bool)(bVar15 >> 1 & 1)) && ((*puVar8 & 0x400000) == 0)) {
              *puVar8 = *puVar8 | 0x40400000;
            }
            uVar11 = _opd_FUN_000cdc90(uVar9);
            uVar9 = *(undefined4 *)(puVar5 + -0x7bc4);
            goto LAB_00b111b4;
          }
          if (puVar10 == &UNK_00f8ca67) {
            uVar11 = _opd_FUN_000cdc90(*(undefined4 *)(puVar5 + -0x7bd8));
            uVar12 = _opd_FUN_000cdc90(*(undefined4 *)(puVar5 + -0x7bd4));
            uVar9 = _opd_FUN_000cdc90(*(undefined4 *)(puVar5 + -0x7bd0));
            if ((bVar1) && ((*puVar6 & 0x400000) == 0)) {
              *puVar6 = *puVar6 | 0x40400000;
            }
            uVar3 = *(undefined4 *)(puVar5 + -0x7c30);
            uVar13 = _opd_FUN_000cdc90(uVar3);
            _opd_FUN_00b30be8(*(undefined4 *)(param_1 + 0x60),0x1c090,uVar13,uVar11);
            uVar11 = *(undefined4 *)(param_1 + 0x60);
            uVar13 = _opd_FUN_00242bf0(uVar11,0x1c090);
            uVar11 = _opd_FUN_00255678(uVar11,uVar13);
            _opd_FUN_00255960(puVar6,uVar11);
            if ((!(bool)(bVar16 >> 1 & 1)) && ((*puVar7 & 0x400000) == 0)) {
              *puVar7 = *puVar7 | 0x40400000;
            }
            uVar11 = _opd_FUN_000cdc90(uVar3);
            _opd_FUN_00b30be8(*(undefined4 *)(param_1 + 0x60),&DAT_00381232,uVar11,uVar12);
            uVar11 = *(undefined4 *)(param_1 + 0x60);
            uVar12 = _opd_FUN_00242bf0(uVar11,&DAT_00381232);
            uVar11 = _opd_FUN_00255678(uVar11,uVar12);
            _opd_FUN_00255960(puVar7,uVar11);
            uVar2 = *(uint *)(param_1 + 0xfd8);
            *(uint *)(param_1 + 0xfdc) =
                 ((int)uVar2 >> 1) + (uint)((int)uVar2 < 0 && (uVar2 & 1) != 0);
            if ((!(bool)(bVar15 >> 1 & 1)) && ((*puVar8 & 0x400000) == 0)) {
              *puVar8 = *puVar8 | 0x40400000;
            }
            uVar11 = _opd_FUN_000cdc90(uVar3);
            goto LAB_00b111c8;
          }
        }
      }
      else {
        if (puVar10 == &UNK_00f8cae7) {
          if (bVar1) {
            if ((*puVar6 & 0x400000) == 0) {
              *puVar6 = *puVar6 | 0x40400000;
            }
          }
          uVar11 = _opd_FUN_000cdc90(*(undefined4 *)(puVar5 + -0x7c30));
          uVar9 = *(undefined4 *)(puVar5 + -0x7bdc);
          goto LAB_00b11720;
        }
        if ((int)puVar10 < 0xf8cae8) {
          if (puVar10 == &UNK_00f8cac7) {
            if (bVar1) {
              if ((*puVar6 & 0x400000) == 0) {
                *puVar6 = *puVar6 | 0x40400000;
              }
            }
            uVar9 = *(undefined4 *)(puVar5 + -0x7c30);
            uVar11 = _opd_FUN_000cdc90(uVar9);
            uVar12 = _opd_FUN_000cdc90(*(undefined4 *)(puVar5 + -0x7bf0));
            _opd_FUN_00b30be8(*(undefined4 *)(param_1 + 0x60),0x1c090,uVar11,uVar12);
            uVar11 = *(undefined4 *)(param_1 + 0x60);
            uVar12 = _opd_FUN_00242bf0(uVar11,0x1c090);
            uVar11 = _opd_FUN_00255678(uVar11,uVar12);
            _opd_FUN_00255960(puVar6,uVar11);
            if ((!(bool)(bVar16 >> 1 & 1)) && ((*puVar7 & 0x400000) == 0)) {
              *puVar7 = *puVar7 | 0x40400000;
            }
            _opd_FUN_00b18ab0();
            uVar11 = _opd_FUN_000cdc90(uVar9);
            uVar12 = _opd_FUN_000cdc90(*(undefined4 *)(puVar5 + -0x7bec));
            _opd_FUN_00b30be8(*(undefined4 *)(param_1 + 0x60),&DAT_00381232,uVar11,uVar12);
            uVar11 = *(undefined4 *)(param_1 + 0x60);
            uVar12 = _opd_FUN_00242bf0(uVar11,&DAT_00381232);
            uVar11 = _opd_FUN_00255678(uVar11,uVar12);
            _opd_FUN_00255960(puVar7,uVar11);
            uVar2 = *(uint *)(param_1 + 0xfd8);
            *(uint *)(param_1 + 0xfdc) =
                 ((int)uVar2 >> 1) + (uint)((int)uVar2 < 0 && (uVar2 & 1) != 0);
            if ((!(bool)(bVar15 >> 1 & 1)) && ((*puVar8 & 0x400000) == 0)) {
              *puVar8 = *puVar8 | 0x40400000;
            }
            uVar11 = _opd_FUN_000cdc90(uVar9);
            uVar9 = _opd_FUN_000cdc90(*(undefined4 *)(puVar5 + -0x7be8));
            goto LAB_00b111c8;
          }
        }
        else {
          if (puVar10 == (undefined *)0xf8cb07) {
            if (bVar1) {
              if ((*puVar6 & 0x400000) == 0) {
                *puVar6 = *puVar6 | 0x40400000;
              }
            }
            uVar9 = *(undefined4 *)(puVar5 + -0x7c30);
            uVar11 = _opd_FUN_000cdc90(uVar9);
            uVar12 = _opd_FUN_000cdc90(*(undefined4 *)(puVar5 + -0x7bb4));
            _opd_FUN_00b30be8(*(undefined4 *)(param_1 + 0x60),0x1c090,uVar11,uVar12);
            uVar11 = *(undefined4 *)(param_1 + 0x60);
            uVar12 = _opd_FUN_00242bf0(uVar11,0x1c090);
            uVar11 = _opd_FUN_00255678(uVar11,uVar12);
            _opd_FUN_00255960(puVar6,uVar11);
            if ((!(bool)(bVar16 >> 1 & 1)) && ((*puVar7 & 0x400000) == 0)) {
              *puVar7 = *puVar7 | 0x40400000;
            }
            uVar11 = _opd_FUN_000cdc90(uVar9);
            uVar12 = _opd_FUN_000cdc90(*(undefined4 *)(puVar5 + -0x7bb0));
            _opd_FUN_00b30be8(*(undefined4 *)(param_1 + 0x60),&DAT_00381232,uVar11,uVar12);
            uVar11 = *(undefined4 *)(param_1 + 0x60);
            uVar12 = _opd_FUN_00242bf0(uVar11,&DAT_00381232);
            uVar11 = _opd_FUN_00255678(uVar11,uVar12);
            _opd_FUN_00255960(puVar7,uVar11);
            uVar2 = *(uint *)(param_1 + 0xfd8);
            *(uint *)(param_1 + 0xfdc) =
                 ((int)uVar2 >> 1) + (uint)((int)uVar2 < 0 && (uVar2 & 1) != 0);
            if ((!(bool)(bVar15 >> 1 & 1)) && ((*puVar8 & 0x400000) == 0)) {
              *puVar8 = *puVar8 | 0x40400000;
            }
            uVar11 = _opd_FUN_000cdc90(uVar9);
            uVar9 = *(undefined4 *)(puVar5 + -0x7bac);
            goto LAB_00b111b4;
          }
          if (puVar10 == (undefined *)0xf8cb67) {
            if (bVar1) {
              if ((*puVar6 & 0x400000) == 0) {
                *puVar6 = *puVar6 | 0x40400000;
              }
            }
            uVar9 = *(undefined4 *)(puVar5 + -0x7c30);
            uVar11 = _opd_FUN_000cdc90(uVar9);
            uVar12 = _opd_FUN_000cdc90(*(undefined4 *)(puVar5 + -0x7ba8));
            _opd_FUN_00b30be8(*(undefined4 *)(param_1 + 0x60),0x1c090,uVar11,uVar12);
            uVar11 = *(undefined4 *)(param_1 + 0x60);
            uVar12 = _opd_FUN_00242bf0(uVar11,0x1c090);
            uVar11 = _opd_FUN_00255678(uVar11,uVar12);
            _opd_FUN_00255960(puVar6,uVar11);
            if ((!(bool)(bVar16 >> 1 & 1)) && ((*puVar7 & 0x400000) == 0)) {
              *puVar7 = *puVar7 | 0x40400000;
            }
            uVar11 = _opd_FUN_000cdc90(uVar9);
            uVar9 = *(undefined4 *)(puVar5 + -0x7ba4);
            goto LAB_00b118b8;
          }
        }
      }
LAB_00b11380:
      _opd_FUN_00b30830(*(undefined4 *)(param_1 + 0x60),&DAT_002b93f1);
      goto LAB_00b11220;
    }
    if (bVar1) {
      if ((*puVar6 & 0x400000) == 0) {
        *puVar6 = *puVar6 | 0x40400000;
      }
    }
    uVar11 = _opd_FUN_000cdc90(*(undefined4 *)(puVar5 + -0x7c30));
    uVar9 = *(undefined4 *)(puVar5 + -0x7be4);
  }
LAB_00b11720:
  uVar9 = _opd_FUN_000cdc90(uVar9);
  _opd_FUN_00b30be8(*(undefined4 *)(param_1 + 0x60),0x1c090,uVar11,uVar9);
  uVar9 = *(undefined4 *)(param_1 + 0x60);
  uVar11 = _opd_FUN_00242bf0(uVar9,0x1c090);
  uVar9 = _opd_FUN_00255678(uVar9,uVar11);
  _opd_FUN_00255960(puVar6,uVar9);
LAB_00b11220:
  uVar9 = *(undefined4 *)(puVar5 + -0x7b9c);
  puVar4 = (undefined4 *)(*(uint *)(puVar5 + -0x7ba0) & 0xfffffff0);
  uVar11 = *puVar4;
  uStack_6c = puVar4[1];
  uVar12 = puVar4[2];
  uStack_64 = puVar4[3];
  puVar4 = (undefined4 *)(param_1 + 0xfc0U & 0xfffffff0);
  *puVar4 = uVar11;
  puVar4[1] = uStack_6c;
  puVar4[2] = uVar12;
  puVar4[3] = uStack_64;
  puVar4 = (undefined4 *)(param_1 + 0xfb0U & 0xfffffff0);
  *puVar4 = uVar11;
  puVar4[1] = uStack_6c;
  puVar4[2] = uVar12;
  puVar4[3] = uStack_64;
  uVar9 = _opd_FUN_000cdc90(uVar9);
  _opd_FUN_001a6020(param_1 + 0xfc0U,uVar9);
  uVar9 = _opd_FUN_000cdc90(*(undefined4 *)(puVar5 + -0x7b98));
  _opd_FUN_001a6020(param_1 + 0xfb0U,uVar9);
  uVar9 = _opd_FUN_000cdc90(*(undefined4 *)(puVar5 + -0x7b94));
  _opd_FUN_001a6020(&local_70,uVar9);
  *(float *)(param_1 + 0xfd0) = ABS(local_70 - *(float *)(param_1 + 0xfb0));
  *(float *)(param_1 + 0xfd4) = ABS(local_68 - *(float *)(param_1 + 0xfb8));
  return;
}

