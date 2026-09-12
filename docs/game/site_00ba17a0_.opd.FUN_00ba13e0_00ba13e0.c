// hook SITE 0x00ba17a0

void _opd_FUN_00ba13e0(int param_1)

{
  bool bVar1;
  byte bVar2;
  int *piVar3;
  undefined *puVar4;
  int iVar5;
  char cVar6;
  undefined8 uVar7;
  undefined4 *puVar8;
  
  puVar4 = PTR_DAT_0121bd78;
  if ((*(byte *)(param_1 + 0x90) & 1) != 0) {
    iVar5 = _opd_FUN_00246ef8(*(undefined4 *)(param_1 + 100),0xc23126);
    if (iVar5 == 0) {
      _opd_FUN_000c1e00(param_1);
    }
    goto LAB_00ba1440;
  }
  iVar5 = _opd_FUN_00246ef8(*(undefined4 *)(param_1 + 100),0xa9d395);
  if (((iVar5 != 0) ||
      (iVar5 = _opd_FUN_00246ef8(*(undefined4 *)(param_1 + 100),0xe1aa71), iVar5 != 0)) ||
     (iVar5 = _opd_FUN_00246ef8(*(undefined4 *)(param_1 + 100),0xe22a71), iVar5 != 0))
  goto LAB_00ba1440;
  *(char *)(param_1 + 0x91) = *(char *)(param_1 + 0x92);
  if (*(char *)(param_1 + 0x92) != '\x02') {
    iVar5 = _opd_FUN_00ac39f0(0,**(undefined2 **)(puVar4 + -0x7f98));
    if (iVar5 != 0) {
LAB_00ba16f8:
      _opd_FUN_00b04ad8(*(undefined4 *)(param_1 + 100),0xc23126);
      *(byte *)(param_1 + 0x90) = *(byte *)(param_1 + 0x90) | 1;
      _opd_FUN_00b30830(*(undefined4 *)(param_1 + 100),0xc6e090);
      goto LAB_00ba1440;
    }
    cVar6 = *(char *)(param_1 + 0x91);
    if (cVar6 == '\x01') {
      bVar1 = *(int *)(param_1 + 0x9c) < 9;
      iVar5 = _opd_FUN_00246ef8(*(undefined4 *)(param_1 + 100),0x3e59b9);
      if ((iVar5 == 0) &&
         (iVar5 = _opd_FUN_00246ef8(*(undefined4 *)(param_1 + 100),0x6c8a3c), iVar5 == 0)) {
        _opd_FUN_00b04ad8(*(undefined4 *)(param_1 + 100),&DAT_003d8095);
      }
      iVar5 = _opd_FUN_00ac3a48(0,0x10010,0);
      if (iVar5 == 0) {
        iVar5 = _opd_FUN_00ac3a48(0,0x40040,0);
        if (iVar5 == 0) {
          iVar5 = _opd_FUN_00ac39f0(0,**(undefined2 **)(puVar4 + -0x7f94));
          if (iVar5 == 0) goto LAB_00ba1440;
          if (*(int *)(param_1 + 0xa4) != 0) {
            *(undefined1 *)(param_1 + 0x92) = 2;
            *(undefined4 *)(param_1 + 0xac) = 0;
            *(undefined4 *)(param_1 + 0xa4) = 0;
            _opd_FUN_00b9e7d8(param_1);
            _opd_FUN_00b04ad8(*(undefined4 *)(param_1 + 100),0xe22a71);
            _opd_FUN_000482b0(0x5d);
            goto LAB_00ba1440;
          }
          goto LAB_00ba16f8;
        }
        if (*(int *)(param_1 + 0xa4) == 0) {
          iVar5 = _opd_FUN_00ac39f0(0,0x40040);
          if (iVar5 != 0) {
            if (bVar1) goto LAB_00ba1a08;
            iVar5 = *(int *)(param_1 + 0xa0);
            if (*(int *)(param_1 + 0x9c) + -8 == iVar5) {
              *(undefined4 *)(param_1 + 0xa0) = 0;
              *(byte *)(param_1 + 0x90) = *(byte *)(param_1 + 0x90) | 0x80;
              *(undefined4 *)(param_1 + 0xa4) = 1;
            }
            else if (iVar5 + 8 < *(int *)(param_1 + 0x9c)) goto LAB_00ba1d94;
            goto LAB_00ba1854;
          }
          iVar5 = *(int *)(param_1 + 0xa0);
          if (*(int *)(param_1 + 0x9c) <= iVar5 + 8) goto LAB_00ba1440;
        }
        else {
          if (bVar1) goto LAB_00ba1c38;
          iVar5 = *(int *)(param_1 + 0xa0);
          if (*(int *)(param_1 + 0x9c) <= iVar5 + 8) {
            bVar2 = *(byte *)(param_1 + 0x90);
            if ((char)bVar2 < '\0') {
              *(byte *)(param_1 + 0x90) = bVar2 & 0x7f;
              *(undefined4 *)(param_1 + 0xa4) = 0;
            }
            else {
              *(undefined4 *)(param_1 + 0xa0) = 0;
              *(byte *)(param_1 + 0x90) = bVar2 | 0x80;
            }
            goto LAB_00ba1854;
          }
        }
LAB_00ba1d94:
        *(int *)(param_1 + 0xa0) = iVar5 + 1;
        *(byte *)(param_1 + 0x90) = *(byte *)(param_1 + 0x90) | 0x80;
      }
      else if (*(int *)(param_1 + 0xa4) == 1) {
        iVar5 = _opd_FUN_00ac39f0(0,0x10010);
        if (iVar5 == 0) {
          iVar5 = *(int *)(param_1 + 0xa0);
          if (iVar5 < 1) goto LAB_00ba1440;
LAB_00ba1dbc:
          *(int *)(param_1 + 0xa0) = iVar5 + -1;
          *(byte *)(param_1 + 0x90) = *(byte *)(param_1 + 0x90) | 0x40;
        }
        else if (bVar1) {
LAB_00ba1c38:
          *(undefined4 *)(param_1 + 0xa4) = 0;
        }
        else {
          iVar5 = *(int *)(param_1 + 0xa0);
          if (iVar5 == 0) {
            *(byte *)(param_1 + 0x90) = *(byte *)(param_1 + 0x90) | 0x40;
            *(int *)(param_1 + 0xa0) = *(int *)(param_1 + 0x9c) + -8;
            *(undefined4 *)(param_1 + 0xa4) = 0;
          }
          else if (0 < iVar5) goto LAB_00ba1dbc;
        }
      }
      else if (bVar1) {
LAB_00ba1a08:
        *(undefined4 *)(param_1 + 0xa4) = 1;
      }
      else {
        iVar5 = *(int *)(param_1 + 0xa0);
        if (iVar5 + 8 == *(int *)(param_1 + 0x9c)) {
          if (iVar5 == 0) {
            *(undefined4 *)(param_1 + 0xa4) = 1;
            *(byte *)(param_1 + 0x90) =
                 *(byte *)(param_1 + 0x90) & 0x80 |
                 (byte)((((ulonglong)*(byte *)(param_1 + 0x90) & 0x3f) << 0x19) >> 0x18) >> 1;
          }
          else {
            *(int *)(param_1 + 0xa0) = iVar5 + -1;
            *(byte *)(param_1 + 0x90) = *(byte *)(param_1 + 0x90) | 0x40;
          }
        }
        else {
          bVar2 = *(byte *)(param_1 + 0x90);
          if ((bVar2 & 0x40) == 0) {
            *(int *)(param_1 + 0xa0) = *(int *)(param_1 + 0x9c) + -8;
            *(byte *)(param_1 + 0x90) = bVar2 | 0x40;
          }
          else if (iVar5 == 0) {
            *(undefined4 *)(param_1 + 0xa4) = 1;
            *(byte *)(param_1 + 0x90) =
                 bVar2 & 0x80 | (byte)((((ulonglong)bVar2 & 0x3f) << 0x19) >> 0x18) >> 1;
          }
          else {
            *(int *)(param_1 + 0xa0) = iVar5 + -1;
          }
        }
      }
LAB_00ba1854:
      _opd_FUN_000482b0(0x5e);
      _opd_FUN_00b9fb40(param_1);
      goto LAB_00ba1440;
    }
    if (cVar6 == '\0') {
      iVar5 = _opd_FUN_00246ef8(*(undefined4 *)(param_1 + 100),0x3e59b9);
      if (iVar5 == 0) {
        _opd_FUN_00b04ad8(*(undefined4 *)(param_1 + 100),&DAT_003d8095);
      }
      iVar5 = _opd_FUN_00ac3a48(0,0x10010,0);
      if ((iVar5 == 0) || (*(int *)(param_1 + 0x94) < 9)) {
        iVar5 = _opd_FUN_00ac3a48(0,0x40040,0);
        if ((iVar5 == 0) || (*(int *)(param_1 + 0x94) < 9)) {
          iVar5 = _opd_FUN_00ac39f0(0,**(undefined2 **)(puVar4 + -0x7f94));
          if (iVar5 != 0) {
            puVar8 = (undefined4 *)(param_1 + 0x68);
            iVar5 = 0;
            *(undefined1 *)(param_1 + 0x92) = 1;
            *(undefined4 *)(param_1 + 0xa4) = 1;
            do {
              _opd_FUN_00b30830(*puVar8,0x6335f4);
              _opd_FUN_002438e0(*puVar8);
              _opd_FUN_0024d078((double)*(float *)(puVar4 + -0x7f90),*puVar8);
              bVar1 = iVar5 != 7;
              puVar8 = puVar8 + 1;
              iVar5 = iVar5 + 1;
            } while (bVar1);
            _opd_FUN_00b9fb40(param_1);
            _opd_FUN_00b04ad8(*(undefined4 *)(param_1 + 100),0xe1aa71);
            _opd_FUN_000482b0(0x5d);
          }
          goto LAB_00ba1440;
        }
        if (*(int *)(param_1 + 0xa4) < *(int *)(param_1 + 0x94) + -8) {
          *(int *)(param_1 + 0xa4) = *(int *)(param_1 + 0xa4) + 1;
          _opd_FUN_000482b0(0x5e);
        }
        else {
          iVar5 = _opd_FUN_00ac39f0(0,0x40040);
          if (iVar5 == 0) goto LAB_00ba1440;
          *(undefined4 *)(param_1 + 0xa4) = 0;
          _opd_FUN_000482b0(0x5e);
        }
      }
      else if (*(int *)(param_1 + 0xa4) < 1) {
        iVar5 = _opd_FUN_00ac39f0(0,0x10010);
        if (iVar5 == 0) goto LAB_00ba1440;
        *(int *)(param_1 + 0xa4) = *(int *)(param_1 + 0x94) + -8;
        _opd_FUN_000482b0(0x5e);
      }
      else {
        *(int *)(param_1 + 0xa4) = *(int *)(param_1 + 0xa4) + -1;
        _opd_FUN_000482b0(0x5e);
      }
      _opd_FUN_00ba08d8(param_1,3);
      goto LAB_00ba1440;
    }
    if (cVar6 != '\x02') goto LAB_00ba1440;
  }
  _opd_FUN_00b30830(*(undefined4 *)(param_1 + 100),0x1b4c98);
  iVar5 = _opd_FUN_00ac3a48(0,0x10010,0);
  if (iVar5 == 0) {
    iVar5 = _opd_FUN_00ac3a48(0,0x40040,0);
    if (iVar5 == 0) {
LAB_00ba19e8:
      bVar1 = false;
    }
    else if (*(int *)(param_1 + 0xac) + 9 < *(int *)(param_1 + 0xa8)) {
      iVar5 = *(int *)(param_1 + 0xa4);
      piVar3 = *(int **)(puVar4 + -0x8000);
      do {
        while( true ) {
          iVar5 = iVar5 + 1;
          uVar7 = 4;
          *(int *)(param_1 + 0xa4) = iVar5;
          if (*piVar3 == 0) goto LAB_00ba1934;
          if (iVar5 != 6) break;
LAB_00ba1ab8:
          cVar6 = _opd_FUN_00f04238(*(undefined4 *)(*piVar3 + 0x60),uVar7);
          if (cVar6 != '\0') goto LAB_00ba1934;
          iVar5 = *(int *)(param_1 + 0xa4);
        }
        uVar7 = 5;
        if (iVar5 == 0xf) goto LAB_00ba1ab8;
      } while (iVar5 == 5);
LAB_00ba1934:
      bVar1 = true;
      *(int *)(param_1 + 0xac) = *(int *)(param_1 + 0xac) + 1;
    }
    else {
      iVar5 = _opd_FUN_00ac39f0(0,0x40040);
      if (iVar5 == 0) goto LAB_00ba19e8;
      bVar1 = true;
      *(undefined4 *)(param_1 + 0xa4) = 0;
      *(undefined4 *)(param_1 + 0xac) = 0;
    }
  }
  else {
    iVar5 = *(int *)(param_1 + 0xa4);
    if (iVar5 < 1) {
      iVar5 = _opd_FUN_00ac39f0(0,0x10010);
      if (iVar5 == 0) goto LAB_00ba19e8;
      iVar5 = 0;
      *(undefined4 *)(param_1 + 0xa4) = 0;
      *(undefined4 *)(param_1 + 0xac) = 0;
      if (9 < *(int *)(param_1 + 0xa8)) {
        piVar3 = *(int **)(puVar4 + -0x8000);
LAB_00ba1b44:
        do {
          while( true ) {
            iVar5 = iVar5 + 1;
            *(int *)(param_1 + 0xa4) = iVar5;
            if (*piVar3 == 0) goto LAB_00ba1b70;
            if (iVar5 != 6) break;
            uVar7 = 4;
LAB_00ba1ba4:
            cVar6 = _opd_FUN_00f04238(*(undefined4 *)(*piVar3 + 0x60),uVar7);
            if (cVar6 != '\0') goto LAB_00ba1b70;
            iVar5 = *(int *)(param_1 + 0xa4);
          }
          if (iVar5 == 0xf) {
            uVar7 = 5;
            goto LAB_00ba1ba4;
          }
        } while (iVar5 == 5);
LAB_00ba1b70:
        iVar5 = *(int *)(param_1 + 0xac);
        *(int *)(param_1 + 0xac) = iVar5 + 1;
        if (iVar5 + 10 < *(int *)(param_1 + 0xa8)) {
          iVar5 = *(int *)(param_1 + 0xa4);
          goto LAB_00ba1b44;
        }
      }
      bVar1 = true;
    }
    else {
      piVar3 = *(int **)(puVar4 + -0x8000);
      do {
        while( true ) {
          iVar5 = iVar5 + -1;
          uVar7 = 4;
          *(int *)(param_1 + 0xa4) = iVar5;
          if (*piVar3 == 0) goto LAB_00ba1624;
          if (iVar5 != 6) break;
LAB_00ba16d8:
          cVar6 = _opd_FUN_00f04238(*(undefined4 *)(*piVar3 + 0x60),uVar7);
          if (cVar6 != '\0') goto LAB_00ba1624;
          iVar5 = *(int *)(param_1 + 0xa4);
        }
        uVar7 = 5;
        if (iVar5 == 0xf) goto LAB_00ba16d8;
      } while (iVar5 == 5);
LAB_00ba1624:
      bVar1 = true;
      *(int *)(param_1 + 0xac) = *(int *)(param_1 + 0xac) + -1;
    }
  }
  iVar5 = _opd_FUN_00ac39f0(0,**(undefined2 **)(puVar4 + -0x7f94));
  if ((iVar5 != 0) || (iVar5 = _opd_FUN_00ac39f0(0,**(undefined2 **)(puVar4 + -0x7f98)), iVar5 != 0)
     ) {
    *(undefined1 *)(param_1 + 0x92) = 1;
    *(undefined4 *)(param_1 + 0xa4) = 1;
    _opd_FUN_00b04ad8(*(undefined4 *)(param_1 + 100),0xe1aa71);
    _opd_FUN_00b9fb40(param_1);
    _opd_FUN_000482b0(0x5c);
  }
  if (bVar1) {
    _opd_FUN_00b9e7d8(param_1);
    _opd_FUN_000482b0(0x5e);
  }
LAB_00ba1440:
  if (*(char *)(param_1 + 0x92) == '\0') {
    _opd_FUN_0024d078((double)*(float *)(puVar4 + -0x7f90),*(undefined4 *)(param_1 + 0x68));
    _opd_FUN_0024d078((double)*(float *)(puVar4 + -0x7f90),*(undefined4 *)(param_1 + 0x6c));
    _opd_FUN_0024d078((double)*(float *)(puVar4 + -0x7f90),*(undefined4 *)(param_1 + 0x70));
    _opd_FUN_0024d078((double)*(float *)(puVar4 + -0x7f90),*(undefined4 *)(param_1 + 0x74));
    _opd_FUN_0024d078((double)*(float *)(puVar4 + -0x7f90),*(undefined4 *)(param_1 + 0x78));
    _opd_FUN_0024d078((double)*(float *)(puVar4 + -0x7f90),*(undefined4 *)(param_1 + 0x7c));
    _opd_FUN_0024d078((double)*(float *)(puVar4 + -0x7f90),*(undefined4 *)(param_1 + 0x80));
    _opd_FUN_0024d078((double)*(float *)(puVar4 + -0x7f90),*(undefined4 *)(param_1 + 0x84));
  }
  _opd_FUN_0024d078((double)*(float *)(puVar4 + -0x7f90),*(undefined4 *)(param_1 + 100));
  return;
}

