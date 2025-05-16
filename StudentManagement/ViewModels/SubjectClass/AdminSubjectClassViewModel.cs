using StudentManagement.Commands;
using StudentManagement.Models;
using StudentManagement.Objects;
using StudentManagement.Services;
using StudentManagement.Utils;
using StudentManagement.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using static StudentManagement.Services.LoginServices;

namespace StudentManagement.ViewModels
{
    public class AdminSubjectClassViewModel : BaseViewModel
    {
        #region properties
        static private ObservableCollection<SubjectClassCard> _storedSubjectClassCards = new ObservableCollection<SubjectClassCard>();
        public static ObservableCollection<SubjectClassCard> StoredSubjectClassCards { get => _storedSubjectClassCards; set => _storedSubjectClassCards = value; }

        private static ObservableCollection<SubjectClassCard> _subjectClassCards = new ObservableCollection<SubjectClassCard>();

        public static ObservableCollection<SubjectClassCard> SubjectClassCards { get => _subjectClassCards; set => _subjectClassCards = value; }

        public VietnameseStringNormalizer vietnameseStringNormalizer = VietnameseStringNormalizer.Instance;

        public bool IsFirstSearchButtonEnabled
        {
            get { return _isFirstSearchButtonEnabled; }
            set
            {
                _isFirstSearchButtonEnabled = value;
                OnPropertyChanged();
            }
        }

        private bool _isFirstSearchButtonEnabled = false;
        private bool _inLoadingSubjectClass = false;
        public bool InLoadingSubjectClass
        {
            get => _inLoadingSubjectClass; set
            {
                _inLoadingSubjectClass = value;
                OnPropertyChanged();
            }
        }

        private string _searchQuery = "";
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                _searchQuery = value;
                SearchSubjectClassCardsFunction();
                OnPropertyChanged();
            }
        }
        private bool _isSaveComplete;
        public bool IsSaveComplete
        {
            get => _isSaveComplete;
            set
            {
                _isSaveComplete = value;
                OnPropertyChanged();
            }
        }

        private Semester _selectedSemester;

        public Semester SelectedSemester
        {
            get => _selectedSemester; set
            {
                _selectedSemester = value;
                LoadSubjectClassListBySemester();
                OnPropertyChanged();
            }
        }

        private ObservableCollection<Semester> _semesters;

        public ObservableCollection<Semester> Semesters { get => _semesters; set => _semesters = value; }

        #endregion

        #region icommand
        public ICommand SwitchSearchButton { get => _switchSearchButton; set => _switchSearchButton = value; }

        private ICommand _switchSearchButton;

        public ICommand SearchSubjectClassCards { get => _searchSubjectClassCards; set => _searchSubjectClassCards = value; }

        private ICommand _searchSubjectClassCards;

        public ICommand ShowSubjectClassDetail { get; set; }


        private ICommand _synchronizeSubjectClass;
        public ICommand SynchronizeSubjectClass { get => _synchronizeSubjectClass; set => _synchronizeSubjectClass = value; }


        #endregion

        public AdminSubjectClassViewModel()
        {
            LoginServices.UpdateCurrentUser += UpdateCurrentUser;

            LoadSubjectClassCards();

            SwitchSearchButton = new RelayCommand<UserControl>((p) => { return true; }, (p) => SwitchSearchButtonFunction(p));
            SearchSubjectClassCards = new RelayCommand<object>((p) => { return true; }, (p) => SearchSubjectClassCardsFunction());
            ShowSubjectClassDetail = new RelayCommand<UserControl>((p) => { return p != null; }, (p) => ShowSubjectClassDetailFunction(p));

            SynchronizeSubjectClass = new RelayCommand<UserControl>((p) => { return true; }, (p) => { LoadSubjectClassCards(); LoadSemesters(); });
        }

        #region methods
        public async Task LoadSubjectClassCards()
        {
            InLoadingSubjectClass = true;

            var subjectClasses = LoadSubjectClassListByRole();

            // reload after trigger in db
            subjectClasses.ForEach(el => DataProvider.Instance.Database.Entry(el).Reload());

            StoredSubjectClassCards.Clear();

            subjectClasses.ForEach(subjectClass => StoredSubjectClassCards.Add(SubjectClassServices.Instance.ConvertSubjectClassToSubjectClassCard(subjectClass)));

            LoadSubjectClassListBySemester();

            LoadSemesters();


            await Task.Delay(1000).ContinueWith((task) => { InLoadingSubjectClass = false; });

            InLoadingSubjectClass = false;
            #region temporary code
            
            #endregion
        }

        public void LoadSemesters()
        {
            SelectedSemester = null;
            Semesters = new ObservableCollection<Semester>(DataProvider.Instance.Database.Semester);
        }

        public void LoadSubjectClassListBySemester()
        {
            SubjectClassCards.Clear();

            foreach (var subjectClass in StoredSubjectClassCards)
            {
                if (subjectClass.SelectedSemester == SelectedSemester || SelectedSemester == null)
                    SubjectClassCards.Add(subjectClass);
            }
        }

        public List<SubjectClass> LoadSubjectClassListByRole()
        {
            var subjectClasses = SubjectClassServices.Instance.LoadSubjectClassList().Where(el => el.IsDeleted != true);

            if (LoginServices.CurrentUser != null)
            {
                switch (LoginServices.CurrentUser.UserRole.Role)
                {
                    case "Admin":
                        return subjectClasses.Where(el => true).ToList();
                    case "Giáo viên":
                        List<SubjectClass> listSubjectClassTeacher = SubjectClassServices.Instance.MinimizeSubjectClassListBySemesterStatus(subjectClasses.ToList(), new bool[] { true, true, true });
                        Teacher currentTeacher = TeacherServices.Instance.GetTeacherbyUser(LoginServices.CurrentUser);
                        return listSubjectClassTeacher.Where(subjectClass => subjectClass.Teacher.FirstOrDefault() == currentTeacher).ToList();
                    default:
                        Student currentStudent = StudentServices.Instance.FindStudentByUserId(LoginServices.CurrentUser.Id);
                        List<SubjectClass> listSubjectClassStudent = CourseRegisterServices.Instance.LoadCourseRegisteredListByStudentId(currentStudent.Id).ToList();
                        listSubjectClassStudent = SubjectClassServices.Instance.MinimizeSubjectClassListBySemesterStatus(listSubjectClassStudent, new bool[] { true, true, true });
                        return listSubjectClassStudent;
                }
            }
            return new List<SubjectClass>();
        }

        public void SwitchSearchButtonFunction(UserControl p)
        {
            IsFirstSearchButtonEnabled = !IsFirstSearchButtonEnabled;
        }

        public void SearchSubjectClassCardsFunction()
        {
            var tmp = StoredSubjectClassCards.Where(x => !IsFirstSearchButtonEnabled ?
                                                    vietnameseStringNormalizer.Normalize(x?.SelectedSubject?.DisplayName + " " + x?.Code).Contains(vietnameseStringNormalizer.Normalize(SearchQuery))
                                                    : vietnameseStringNormalizer.Normalize(x?.SelectedTeacher?.Users?.DisplayName).Contains(vietnameseStringNormalizer.Normalize(SearchQuery)));
            SubjectClassCards.Clear();
            foreach (SubjectClassCard card in tmp)
                if (card.SelectedSemester == SelectedSemester || SelectedSemester == null)
                    SubjectClassCards.Add(card);
        }

        public void ShowSubjectClassDetailFunction(UserControl cardComponent)
        {
            try
            {
                SubjectClassCard card = cardComponent.DataContext as SubjectClassCard;
                SubjectClassDetail subjectClassDetail = new SubjectClassDetail
                {
                    DataContext = new SubjectClassDetailViewModel(card)
                };
                subjectClassDetail.Show();
            }
            catch (Exception)
            {
                MyMessageBox.Show("Đã có lỗi xảy ra! Không thể đến lớp học", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
        #endregion methods

        #region eventhandler
        private void UpdateCurrentUser(object sender, LoginEvent e)
        {
            LoadSemesters();
            LoadSubjectClassCards();
        }
        #endregion
    }
}
